# FinanceDB

A small database for tracking financial transactions. It stores records organized by account using a B-tree structure.

## Architecture

The project is split into three parts:

- **FinanceDB.Server** - HTTP server that hosts the B-tree database
- **FinanceDB.Cli** - Command-line client that talks to the server
- **FinanceDB.Core** - Shared library with models and storage logic

The CLI does not access the database directly. All operations go through HTTP requests to the server.

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
