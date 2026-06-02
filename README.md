# Mortgage Rule Engine

An interactive, high-performance rule engine built on .NET 9 and FastEndpoints, designed for evaluating mortgage loan eligibility against customizable, dynamic rule sets.

---

## 🛠️ Database Setup

The rule engine is backed by a SQL Server database (`OQMS`). To initialize or update the database tables:

1. Connect to your SQL Server instance.
2. Run the SQL script [create_tables.sql] in your database query editor.

This script creates and configures the following tables:
*   **`Workflows`**: Stores workflow definitions and global parameters.
*   **`WorkflowRules`**: Stores rule definitions, metadata, and testing sample payloads.
*   **`RuleAuditLogs`**: Maintains a full audit history of all creates, updates, and deletes of rules.

---

## 🔍 Core Features & Architecture

### 1. Fail-Fast Expression Validation
To prevent invalid rules from being saved to the database, a validation step runs during rule creation and update operations:
*   Rule expressions are compiled and executed in-memory against the **`RuleInput`** schema (defined in `Models/RuleInput.cs`).
*   If the rule contains syntax errors or references a property not defined in `RuleInput`, the operation fails instantly and returns a `400 Bad Request` containing the specific compilation error to the user.

### 2. Automated Field Discovery & Sample JSON Sync
*   When a rule is created or modified, the system automatically parses the expression to extract all required fields.
*   These fields are then merged with the rule's **`SampleJson`** payload. Any new fields are added as `null` placeholders, while all user-provided test values are preserved. This simplifies adhoc testing from the UI dashboard.

### 3. Detailed Change Auditing
*   Every modify operation logs changes to the `RuleAuditLogs` table, detailing what fields were changed, their previous values, and their new values, along with user identification and timestamps.

---

## 🚀 Running the Project

1. Run the application:
   ```powershell
   dotnet run
   ```
2. Open your browser and navigate to:
   ```
   http://localhost:5000
   ```
