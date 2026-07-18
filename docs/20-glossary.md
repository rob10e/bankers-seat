# Glossary

**Account**  
A balance holder. Usually a player, the bank, or a template-defined shared account.

**Action**  
A template-defined named operation such as payday, fee collection, or field update.

**Actor**  
The participant who submits a command.

**Banker**  
A role permitted to execute selected bank operations without necessarily owning the room.

**Command**  
A request to change shared state.

**Correction**  
A new compensating transaction that reverses or adjusts an earlier transaction without deleting history.

**Denomination**  
Presentation metadata representing familiar values such as 1, 5, 10, or 100.

**Edition**  
A distinct version of a physical game's published rules/components represented as part of template identity.

**Event**  
A versioned statement that a command was accepted and state changed.

**Field**  
A custom template-defined player value such as owns-home, children-count, or career.

**Host**  
The participant with session-management authority.

**Idempotency key**  
A client-generated unique value preventing a retried command from being applied twice.

**Ledger**  
The append-only history of accepted financial transactions.

**Posting**  
One signed account mutation within a ledger transaction.

**Room code**  
A short value used to locate and join a session. It is not by itself a durable secret.

**Session**  
One running or completed instance of a game created from a template snapshot.

**Session version**  
A monotonically increasing number assigned to accepted state changes.

**Template**  
A validated JSON definition of banker-related game configuration.

**Template snapshot**  
The immutable copy of a template stored with a session.

**Template version**  
The semantic version of a template's content.

**Visibility**  
The rule controlling who may receive and view a custom field.

**Web app**  
The responsive React application delivered through a browser.

**Hybrid app**  
The same web application packaged in a native shell, planned with Capacitor.
