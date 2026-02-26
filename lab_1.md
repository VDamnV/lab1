🧪 Laboratory Work 1: Messaging System Design
Variant 3 — Offline Message Delivery

🎯 Context
Проєктування мінімальної системи обміну повідомленнями з акцентом на асинхронну доставку та гарантоване збереження повідомлень для користувачів, які тривалий час перебувають в офлайні. Повідомлення не повинні бути втрачені незалежно від того, як довго отримувач не був у мережі.

🧱 Part 1 — Component Diagram
Оскільки користувачі можуть бути офлайн тривалий час, система використовує гібридний підхід: чергу для спроб швидкої доставки (Push) та базу даних як надійне джерело істини для подальшої синхронізації (Pull), коли користувач повертається в мережу.

graph TD
  Client[Client App] -->|HTTPS / WebSocket| API[API Gateway]
  API --> Auth[Auth Service]
  API --> MsgService[Message Service]
  
  MsgService --> DB[(Persistent DB)]
  MsgService --> Queue[Message Queue]
  
  Queue --> DeliveryService[Delivery Service]
  DeliveryService --> Presence[(Presence Cache)]
  
  DeliveryService -- "User Online (Push)" --> Client
  DeliveryService -- "User Offline (Update Status)" --> DB
  
  Client -- "Wake Up Sync (Pull)" --> API

🔁 Part 2 — Sequence Diagram
Сценарій: Користувач A відправляє повідомлення Користувачу B, який наразі офлайн. Пізніше Користувач B підключається до мережі та отримує (синхронізує) свої повідомлення.

sequenceDiagram
  participant A as User A
  participant ClientA as Client A
  participant API
  participant Msg as Message Service
  participant DB
  participant Queue
  participant Delivery as Delivery Service
  participant ClientB as Client B

  A->>ClientA: Send message
  ClientA->>API: POST /messages
  API->>Msg: createMessage()
  Msg->>DB: save(status: "Stored")
  Msg->>Queue: enqueue delivery task
  API-->>ClientA: 202 Accepted

  Queue->>Delivery: process message delivery
  Delivery->>Delivery: Check recipient presence
  Note over Delivery, ClientB: Recipient is OFFLINE
  Delivery->>DB: update(status: "PendingSync")
  
  Note over ClientB, API: Hours later... User B comes online
  
  ClientB->>API: GET /messages/sync (App wakeup)
  API->>Msg: getUndeliveredMessages(User B)
  Msg->>DB: fetch(status: "PendingSync")
  DB-->>Msg: [messages list]
  Msg-->>API: [messages list]
  API-->>ClientB: 200 OK (messages)
  
  ClientB->>API: POST /messages/ack (IDs)
  API->>Msg: markAsDelivered()
  Msg->>DB: update(status: "Delivered")

🔄 Part 3 — State Diagram
Життєвий цикл повідомлення (Message) з урахуванням того, що система орієнтована на тривалі офлайн-періоди.

stateDiagram-v2
  [*] --> Created
  Created --> Stored : Saved to DB
  
  Stored --> DeliveryAttempted : Pushed to Queue
  
  DeliveryAttempted --> Delivered : Recipient Online (Push)
  DeliveryAttempted --> PendingSync : Recipient Offline (Stored for Pull)
  
  PendingSync --> Delivered : Client Reconnects & Syncs
  
  Delivered --> Read
  Read --> [*]

📚 Part 4 — ADR (Architecture Decision Record)

# ADR-001: Hybrid Message Delivery Strategy (Push + Sync) for Offline Users

## Status
Accepted

## Context
Our messenger must ensure reliable delivery even if users remain offline for extended periods (days or weeks). Relying purely on a Message Queue for delivery (keeping messages in the queue until the user comes online) is dangerous: queues can overflow, messages might expire (TTL), and it makes queue management heavily stateful and expensive.

## Decision
We will use a **Hybrid Delivery Mechanism**:
1. **Push Mechanism (via Queue)**: When a message is sent, it is briefly placed in a queue for immediate delivery attempt if the user is currently online.
2. **Pull/Sync Mechanism (via DB)**: If the Delivery Service detects the user is offline, the message is marked as "PendingSync" in the persistent database and *removed* from the active message queue. When the offline user's application wakes up or reconnects, it will explicitly query the API (Pull) to synchronize all missed messages.

## Alternatives Considered
- **Pure Message Queue (e.g., MQTT/RabbitMQ offline queues)**: Rejected. Keeping messages in transient queues for long periods risks data loss upon broker restarts and scales poorly for millions of offline users.
- **Client Polling Only**: Rejected. Constant polling when users are online drains battery and wastes server resources.

## Consequences
+ **Positive**: No messages are lost due to queue eviction. Database acts as a reliable source of truth. Queue remains lightweight and fast.
- **Negative**: Increased complexity on the client side, as it now needs to implement a robust synchronization logic (`GET /messages/sync`) upon every network reconnection, rather than just passively listening.
