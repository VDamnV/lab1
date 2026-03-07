# Messenger API Prototype (Lab 2)

This project is a functional prototype of a messaging system implemented in **C# (ASP.NET Core Web API)**. The architecture is strictly based on the design created in **Lab 1** and incorporates elements of offline message processing as defined in **ADR-003**.

---

## 🚀 Project Description and Implemented Features
The project implements the **Minimal Reference Architecture** with the following capabilities:
* **User Creation:** `POST /users` endpoint to register new participants.
* **Message Sending:** `POST /messages` with validation logic to ensure reliable delivery.
* **Chat History Retrieval:** `GET /conversations/{id}/messages` to return conversation history.
* **Data Persistence:** Uses **Entity Framework Core** and a **SQLite** database (`messenger.db`) to ensure messages are not lost after the program stops.
* **Lab 1 Architecture Support:** Messages are stored with the status `PersistedInQueue`, simulating the behavior of a message broker for users who are currently offline.



---

## 📂 Project Structure
The project is organized into logical modules to ensure separation of responsibilities:
* `/Messenger.Api/Models` – Database entities: `User`, `Message`, and `Conversation`.
* `/Messenger.Api/Storage` – EF Core database context: `ApplicationDbContext`.
* `/Messenger.Api/Services` – Business logic: `MessageService` handling validation and persistence.
* `/Messenger.Api/Controllers` – HTTP routes and API endpoints.
* `/Messenger.Tests/IntegrationTests` – End-to-end integration tests using `xUnit`.
* `postman_collection.json` – A collection for manual API testing and verification.

---

## 🛠 How to Run the Program

1.  Open your terminal in the project's root folder.
2.  Navigate to the API folder:
    ```bash
    cd Messenger.Api
    ```
3.  Run the server:
    ```bash
    dotnet run
    ```
    *(The `messenger.db` database will be created automatically on the first run)*.
4.  For testing, import the `postman_collection.json` file into **Postman** and send requests to the URL provided by GitHub Codespaces.

### Running Tests
To execute the integration test that verifies the full message flow, run:
```bash
dotnet test
