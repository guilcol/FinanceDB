# FinanceDB

A personal finance database built from scratch. It uses a custom B-tree implementation to store and query financial transactions efficiently.

## What it does

- Stores transactions with date, description, and amount
- Organizes data by account (checking, savings, credit cards, etc.)
- Calculates running balances at any point in time
- Imports bank statements from QFX/OFX files
- Persists data as JSON files on disk

## Architecture

The system runs as a client-server application:

- **FinanceDB.Server** - HTTP server hosting the B-tree database. Handles concurrent requests with per-account locking.
- **FinanceDB.Cli** - Command-line client. Parses commands and sends HTTP requests to the server.
- **FinanceDB.Core** - Shared library containing the B-tree implementation, storage logic, and data models.

The CLI does not access the database directly. All operations go through HTTP requests to the server. This separation allows multiple clients to connect to the same database and prepares the architecture for future authentication and multi-user support.

## How to run

Start the server first, then the CLI in a separate terminal.

**Terminal 1 - Server:**
```
cd FinanceDB.Server
dotnet run
```

**Terminal 2 - CLI:**
```
cd FinanceDB.Cli
dotnet run
```

## Commands

| Command | What it does |
|---------|--------------|
| `insert` | Add a new transaction |
| `update` | Change an existing transaction |
| `delete` | Remove a transaction |
| `delete_range` | Remove transactions in a date/sequence range |
| `list` | Show all transactions for an account |
| `list_range` | Show transactions in a date/sequence range |
| `balance` | Show the total for an account |
| `file_import` | Import transactions from a QFX file |
| `save` | Save data to files |
| `exit` | Save and quit |

## Examples

Add a transaction (uses current time):
```
insert checking "Coffee" 5.50
```

Add a transaction with a specific date:
```
insert checking 2024-01-15T10:30:00Z "Groceries" 45.00
```

See all transactions:
```
list checking
```

Check the balance:
```
balance checking
```

Import from a bank file:
```
file_import checking C:\Downloads\statement.qfx
```

## Configuration

**Server** (`FinanceDB.Server/appsettings.json`):
- `BTree:Degree` - Controls B-tree node size (default: 100)
- Listens on `http://localhost:5000`

**CLI** (`FinanceDB.Cli/appsettings.json`):
- `Server:BaseUrl` - Server address (default: `http://localhost:5000`)

## How data is stored

Each record has a key with three fields:
- **AccountId** - which account (e.g., "checking")
- **Date** - when the transaction happened
- **Sequence** - distinguishes multiple records on the same date

Records are stored in B-trees (one per account):

```
                    [Root Node]
                   /     |     \
            [Node A]  [Node B]  [Node C]
            /    \       |        /   \
        [Leaf] [Leaf] [Leaf]  [Leaf] [Leaf]
```

- **Leaf nodes** hold the actual records
- **Internal nodes** hold references to child nodes with key ranges and subtree balances
- Each node is saved as a JSON file in `Nodes/<accountId>/`
