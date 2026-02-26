## Part 1 — Component Diagram

```mermaid
flowchart LR
    subgraph Clients
        Sender[Sender Client\n(Mobile/Web)]
        Receiver[Receiver Client\n(Often Offline)]
    end

    API[Backend API Gateway]
    Auth[Auth Service]
    MsgService[Message Service]
    DB[(Main Database)]
    Queue[[Message Broker\nRabbitMQ / SQS]]
    PushService[Push Notification\nAPNs / FCM]

    Sender -- "1. Send Message (HTTP/REST)" --> API
    API -- "2. Validate Token" --> Auth
    API -- "3. Forward" --> MsgService
    MsgService -- "4. Save Metadata" --> DB
    MsgService -- "5. Enqueue for Receiver" --> Queue
    
    Queue -- "6. Trigger Wake-up" --> PushService
    PushService -. "7. Silent Push" .-> Receiver
    Receiver -- "8. Connect & Fetch (WebSocket)" --> Queue
    Receiver -- "9. ACK Delivery" --> Queue
```markdown
##State Diagram
```mermaid
stateDiagram-v2
    [*] --> Created: User types message
    Created --> Sending: Client initiates request
    Sending --> FailedSend: Network disconnected
    FailedSend --> RetryingSend: Exponential backoff
    RetryingSend --> Sending: Network restored
    
    Sending --> PersistedInQueue: Server accepts & stores
    
    PersistedInQueue --> PushTriggered: Receiver is offline
    PushTriggered --> Delivering: Device wakes up
    PersistedInQueue --> Delivering: Receiver is online (WebSocket open)
    
    Delivering --> FailedDelivery: Connection dropped
    FailedDelivery --> PersistedInQueue: Message stays in queue
    
    Delivering --> Delivered: Client saves locally & sends ACK
    
    Delivered --> Read: User opens chat
    Read --> [*]

# ADR-003 Offline Message Delivery Architecture

| Field             | Value                          |
|-------------------|--------------------------------|
| Status            | Proposed                       |
| Date              | 2026-02-26                     |
| Decision Makers   | Architecture Team              |
| Technical Area    | Backend / Messaging            |

## Context

* The system requires a highly reliable messaging infrastructure where messages must not be lost under any circumstances.
* Users (especially on mobile devices) can be offline or have unstable network connections for extended periods (hours or even days).
* Synchronous HTTP/REST calls frequently fail due to client unreachability.
* We need to determine the optimal strategy for delivering messages when the client reconnects: client-side polling vs. server-side queueing with active push mechanisms.
* A robust retry strategy is required to handle transient network errors without causing thundering herd problems when thousands of clients reconnect simultaneously.
* Battery life and data usage on mobile clients must be optimized (frequent polling drains the battery).

## Decision

Implement an **Asynchronous Queue-Based Delivery System** with **WebSocket synchronization** and **Push Notification Wake-ups**, utilizing an Exponential Backoff retry strategy.

### Persistent Queues + WebSocket Sync + Client Local Storage

Details:
* **Server-Side Storage:** Use a robust message broker (e.g., RabbitMQ or AWS SQS) to persist messages securely while the recipient is offline. 
* **Client Local Storage:** Mobile/web clients will use local databases (e.g., SQLite, CoreData, or IndexedDB) to store outgoing messages locally if the network is unavailable, and queue them for upstream delivery.
* **Delivery Mechanism (No Polling):** * Instead of client polling (which drains battery and wastes server resources), the backend will send a silent Push Notification (FCM/APNs) to wake up the client app when a new message arrives.
  * Upon wake-up or app foregrounding, the client establishes a WebSocket connection to pull pending messages from the user's dedicated queue.
* **Retry Strategy (Exponential Backoff):** * If message delivery (either upstream to the server or downstream to the client) fails, retries will use exponential backoff (e.g., 2s, 4s, 8s, 16s, up to a max limit) with jitter to prevent server overload.
* **Acknowledgment (ACK):** Messages are strictly kept in the server queue until the client explicitly sends an ACK confirming local persistence. If the WebSocket drops before the ACK, the message remains in the queue for the next sync.

```mermaid
 %%{init: { 'theme':'base'} }%%
sequenceDiagram
    participant SenderApp
    participant Backend
    participant MessageBroker
    participant PushService
    participant ReceiverApp

    Note over ReceiverApp: Receiver is OFFLINE
    SenderApp->>Backend: Send Message(ID: 101)
    Backend->>MessageBroker: Enqueue & Persist Message(ID: 101)
    MessageBroker-->>Backend: Ack Persisted
    Backend-->>SenderApp: 202 Accepted (Sent)
    
    Backend->>PushService: Trigger Wake-up Push Notification
    PushService--xReceiverApp: Fails (Device Offline)

    Note over ReceiverApp: Receiver comes ONLINE
    ReceiverApp->>Backend: Open WebSocket Connection
    Backend->>MessageBroker: Fetch Pending Messages
    MessageBroker-->>Backend: [Message(ID: 101)]
    Backend->>ReceiverApp: Deliver [Message(ID: 101)]
    
    Note over ReceiverApp: Saves to local DB
    ReceiverApp->>Backend: ACK Message(ID: 101)
    Backend->>MessageBroker: Dequeue/Remove Message(ID: 101)
```
Pros:
* **Zero Message Loss:** Messages are safely persisted in the message broker until explicitly acknowledged by the recipient.
* **Resource Efficiency:** Eliminates the need for continuous polling, saving client battery and reducing unnecessary server load.
* **Resilience:** The exponential backoff with jitter smoothly handles mass reconnections (e.g., when a subway train gets cellular service back).
* **Clear State Management:** Distinct responsibilities between the message broker (durability) and the application backend (routing).

Cons:
* **Increased Complexity:** Requires maintaining local databases on clients and handling synchronization logic (resolving race conditions).
* **Infrastructure Overhead:** Requires deploying and maintaining a dedicated Message Broker and integrating Push Notification services.
* **Ordering Guarantees:** Strict FIFO (First-In-First-Out) ordering requires careful partition/queue design per user, which can complicate broker configuration.

## Consequences

* Development teams will need to implement persistent local storage on all client applications.
* Backend infrastructure must be updated to include a scalable message broker.
* A strict ACK/NACK contract must be defined between the client and backend.
* Mobile applications will require configuration for background execution and silent push notifications.

## Migration Plan (High Level)

1. Provision the Message Broker cluster (e.g., RabbitMQ).
2. Update backend messaging services to write to the broker instead of direct client delivery.
3. Implement the WebSocket synchronization endpoint on the backend.
4. Update client applications to utilize local storage (SQLite/IndexedDB) for outbox/inbox patterns.
5. Implement Exponential Backoff interceptors on mobile network clients.
6. Roll out changes incrementally, allowing older client versions to use legacy endpoints until deprecated.

## Potential next steps (not subject to current ADR)

* Implement End-to-End (E2E) encryption for messages resting in the broker.
* Implement a Dead Letter Queue (DLQ) for messages that crash the client or cannot be parsed after maximum retries.
* Add message tombstoning for remote deletion functionality.

## Resources

- Enterprise Integration Patterns: Guaranteed Delivery
- AWS Architecture Blog: Exponential Backoff and Jitter
- RFC 6455: The WebSocket Protocol
