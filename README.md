# FinanceDB

A small database for tracking financial transactions. It stores records organized by account and saves them to files.

## What it does

- Store transactions with a date, description, and amount
- Organize transactions by account (like "checking", "savings", etc.)
- Calculate account balances
- Save and load data from files

## How to run

```
dotnet run --project FinanceDB
```

## Commands

| Command | What it does |
|---------|--------------|
| `insert` | Add a new transaction |
| `update` | Change an existing transaction |
| `delete` | Remove a transaction |
| `delete_range` | Remove all transactions in a date/sequence range |
| `list` | Show all transactions for an account |
| `balance` | Show the total for an account |
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

Delete all transactions in January 2024 (inclusive range):
```
delete_range checking from 2024-01-01T00:00:00Z 0 to 2024-01-31T23:59:59Z 999
```

## How data is stored

Records are stored in a B-tree, a tree structure that keeps data sorted and allows fast lookups.

### Records and keys

Each record has a **RecordKey** made of three fields:
- **AccountId** - which account the record belongs to
- **Date** - when the transaction happened
- **Sequence** - a number to tell apart multiple records with the same date

The Sequence field exists because you might have several transactions on the same day. When you insert a record, the system looks at all existing records for that account and date, finds the highest sequence number, and assigns the next one. If no records exist for that date, it uses 0. This makes every key unique within an account.

### B-tree basics

- The tree is made of **nodes**. Each node holds multiple records.
- Records are sorted by AccountId, then Date, then Sequence.
- When a node gets too full, it **splits** into smaller nodes.
- The tree stays balanced - all leaf nodes are at the same depth.
- The **degree** (set to 100) controls how many records fit in each node before splitting.

### Structure

```
                    [Root Node]
                   /     |     \
            [Node A]  [Node B]  [Node C]
            /    \       |        /   \
        [Leaf] [Leaf] [Leaf]  [Leaf] [Leaf]
```

- **Leaf nodes** hold the actual records.
- **Internal nodes** hold references pointing to child nodes, along with key ranges and subtree balances.
- Each account has its own B-tree.

### Storage on disk

- Each node is saved as a separate JSON file (named by node ID).
- Files live in `Nodes/<accountId>/` folders.
- Nodes are loaded into memory on demand and cached.
- Two storage modes exist: `FileNodeStorage` (persists to disk) and `MemoryNodeStorage` (in-memory only).
