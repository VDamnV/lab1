🧪 Laboratory Work 1: Variant 3
Designing a Messaging System with Focus on Offline Delivery

🧱 Part 1 — Component Diagram
Для забезпечення надійності ми використовуємо архітектуру, що базується на подіях (Event-Driven). Message Store гарантує персистентність, а Notification Service відповідає за пробудження офлайн-клієнтів через Push-сповіщення.

graph TD
    Client_A[Client A] --> API[Backend API]
    Client_B[Client B]
    
    API --> MS[Message Service]
    MS --> DB[(Persistent Store: PostgreSQL/Cassandra)]
    MS --> Queue{Message Broker / Queue}
    
    Queue --> DS[Delivery Service]
    DS --> WS[WebSocket Manager]
    DS --> Push[Push Notification Service]
    
    WS --> Client_B
    Push -- "Trigger" --> Client_B

🔁 Part 2 — Sequence Diagram
Сценарій: Користувач А надсилає повідомлення користувачу Б, який перебуває офлайн. Система зберігає повідомлення та ініціює доставку через Push-сповіщення.

sequenceDiagram
    participant A as User A
    participant API as Backend API
    participant MS as Message Service
    participant DB as Database
    participant Q as Delivery Queue
    participant PS as Push Service
    participant B as User B (Offline)

    A->>API: POST /send_message
    API->>MS: Process Message
    MS->>DB: Save Message (Status: PENDING)
    MS->>Q: Publish Delivery Task
    API-->>A: 202 Accepted (Message Sent)
    
    Note over Q, PS: Delivery Service picks up task
    Q->>PS: User B is offline, send Push
    PS-->>B: Notification: "You have a new message"
    
    Note over B, DB: User B comes online later
    B->>API: GET /sync_messages
    API->>DB: Fetch unread messages
    DB-->>B: Return Messages
    B->>API: ACK (Message Received)
    API->>DB: Update Status (DELIVERED)

🔄 Part 3 — State Diagram
Об'єкт: Message (Життєвий цикл повідомлення в умовах тривалого офлайну).

stateDiagram-v2
    [*] --> Created
    Created --> Stored: Persistence confirmed
    Stored --> PendingDelivery: Added to Queue
    
    state PendingDelivery {
        [*] --> WaitingForUser
        WaitingForUser --> Notifying: Trigger Push
        Notifying --> WaitingForUser: Retry if failed
    }
    
    PendingDelivery --> Delivered: User online & ACK received
    Delivered --> Read: User opened chat
    
    PendingDelivery --> Expired: TTL reached (e.g., 30 days)
    Expired --> [*]
    Read --> [*]

📚 Part 4 — ADR (Architecture Decision Record)

# ADR-003: Store-and-Forward approach with Push Notifications

## Status
Accepted

## Context
Користувачі можуть бути офлайн протягом тривалого часу (дні або тижні). Повідомлення не повинні бути втрачені, а отримувач має дізнатися про них навіть із закритою програмою.

## Decision
Ми впроваджуємо стратегію "Store-and-Forward":
1. Кожне повідомлення обов'язково зберігається в базі даних перед спробою доставки.
2. Використовується черга повідомлень (Message Queue) для асинхронної обробки.
3. Якщо WebSocket-з'єднання з отримувачем відсутнє, система автоматично надсилає Push-сповіщення (Firebase Cloud Messaging або Apple Push Notification service).
4. Доставка вважається успішною лише після отримання прикладного підтвердження (ACK) від клієнта отримувача.

## Alternatives
- **Client Polling:** Відхилено через високе навантаження на сервер та швидке розряджання батареї мобільних пристроїв.
- **In-Memory Queue only:** Відхилено через ризик втрати даних при перезавантаженні сервера.

## Consequences
+ Гарантована доставка (At-least-once delivery).
+ Можливість синхронізації історії на різних пристроях.
- Збільшення затримки (latency) через необхідність запису в БД.
- Необхідність обробки дублікатів повідомлень на стороні клієнта.
