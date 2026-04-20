# Plan de démonstration — Demo Aspire Azure Functions avec Service Bus

## Objectif de la démonstration

Montrer comment **.NET Aspire** simplifie le développement local d'une architecture événementielle en orchestrant automatiquement les émulateurs Azure (Service Bus, Azurite) et les conteneurs (SQL Server), et en intégrant une **Azure Function** déclenchée par un message Service Bus pour persister des données en base.

---

## Prérequis avant la démonstration

Vérifier que tout est prêt **avant** de commencer :

- [ ] Docker Desktop est démarré et opérationnel (`docker info`)
- [ ] Le secret SQL Server est configuré :
  ```powershell
  dotnet user-secrets --project AspireApp.AppHost set "Parameters:sql-sa-password" "VotreMotDePasse123!"
  ```
- [ ] La solution a déjà été lancée une fois (les images Docker sont téléchargées en cache)
- [ ] Visual Studio 2026 est ouvert sur la solution

---

## Déroulement de la démonstration

### Étape 1 — Présentation du contexte (3 min)

**À expliquer :**

- Problématique : développer localement une architecture Azure événementielle nécessite habituellement de provisionner des ressources cloud ou de configurer manuellement des émulateurs.
- Solution : **.NET Aspire** agit comme un orchestrateur local qui démarre, configure et connecte automatiquement toutes les ressources.
- Cas d'usage : une application reçoit des demandes de communication (e-mail, SMS, etc.) via une file Service Bus, les traite via une Azure Function et les persiste dans SQL Server.

---

### Étape 2 — Tour de la solution (5 min)

**Ouvrir et commenter les fichiers suivants dans Visual Studio :**

#### `AspireApp.AppHost/Program.cs`
> Point d'entrée de l'orchestration Aspire.

Points à souligner :
- `.AddAzureServiceBus(...).RunAsEmulator()` → démarre automatiquement l'**émulateur Service Bus** dans Docker, sans aucune configuration manuelle.
- `ContainerLifetime.Persistent` → le conteneur survit aux redémarrages, les messages sont conservés entre les sessions.
- `.AddSqlServer(...)` → démarre un conteneur **SQL Server** avec le mot de passe injecté depuis les secrets utilisateur.
- `.WaitFor(migrator)` → la Function ne démarre qu'une fois le schéma de la BD créé, garantissant l'ordre de démarrage.

#### `AspireApp.AppHost/Extensions/ServiceBusExtensions.cs`
> Commande de test intégrée au tableau de bord Aspire.

Points à souligner :
- `.WithCommand(...)` permet d'ajouter des **actions personnalisées** directement dans le tableau de bord Aspire.
- Envoie un message JSON (`Communication`) dans la file `myqueue` en un seul clic, sans outil externe.

#### `AspireApp.DbMigrator/Program.cs`
> Projet console qui crée le schéma de la base de données au démarrage.

Points à souligner :
- Lit la chaîne de connexion injectée automatiquement par Aspire (`ConnectionStrings__Communication`).
- Crée la table `[dbo].[Communication]` si elle n'existe pas (idempotent).
- S'exécute une seule fois au démarrage, avant la Function.

#### `AspireApp.FunctionApp/ServiceBusFunction.cs`
> Azure Function déclenchée par un message Service Bus.

Points à souligner :
- `[ServiceBusTrigger("myqueue", Connection = "myservicebus")]` → la connexion est injectée automatiquement par Aspire, aucune chaîne de connexion à configurer manuellement.
- Désérialisation du message en `record Communication`.
- Gestion des cas d'erreur : **dead-letter** si désérialisation échoue, **abandon** pour les autres erreurs.
- Insertion en base via `SqlConnection` avec la chaîne de connexion injectée par Aspire.
- Journaux structurés avec `LoggerMessage` (source généré, haute performance).

---

### Étape 3 — Démarrage et tableau de bord Aspire (3 min)

**Lancer la solution depuis Visual Studio** (F5 ou `dotnet run --project AspireApp.AppHost`).

Observer dans la console l'ordre de démarrage :
1. Azurite (stockage local)
2. Service Bus Emulator
3. SQL Server
4. DbMigrator (migration)
5. FunctionApp

**Dans le tableau de bord Aspire :**

- Montrer la liste de toutes les ressources et leur état (`Running` / `Finished`).
- Montrer les **chaînes de connexion** injectées automatiquement dans chaque ressource.
- Montrer les **variables d'environnement** de la Function (connexion Service Bus et SQL injectées par Aspire).

---

### Étape 4 — Envoi d'un message et observation (4 min)

1. Dans le tableau de bord, naviguer vers la ressource **`myqueue`**.
2. Cliquer sur **« Déposer un message dans la Queue »**.
3. Observer en temps réel :
   - Les **journaux** de la ressource `functionapp` → message reçu, traité, inséré.
   - Le message de journal structuré : `Communication {Id} de type {Type} insérée`.

**Payload envoyé :**
```json
{
  "Id": "...",
  "Type": "Email",
  "Destination": "user@example.com",
  "Subject": "Welcome",
  "Body": "Hello from Aspire!",
  "RequestedAtUtc": "..."
}
```

---

### Étape 5 — Exploration des journaux et traces (3 min)

**Dans le tableau de bord Aspire :**

- Onglet **Journaux** de `functionapp` → journaux structurés de chaque exécution.
- Onglet **Traces** → trace distribuée de bout en bout (Service Bus → Function → SQL).
- Onglet **Métriques** → métriques OpenTelemetry collectées automatiquement.

> Insister sur le fait que tout cela est disponible **en local sans configuration**, grâce à `AspireApp.ServiceDefaults` qui configure OpenTelemetry (logs, traces, métriques) de façon centralisée.

---

## Points clés à retenir

| Concept | Ce que la démo illustre |
|---|---|
| **Orchestration locale** | Aspire démarre et connecte tous les services automatiquement |
| **Zéro configuration manuelle** | Chaînes de connexion injectées, ordre de démarrage géré |
| **Émulateurs intégrés** | Service Bus Emulator + Azurite + SQL Server via Docker |
| **Commandes de test** | Actions personnalisées dans le tableau de bord sans outil externe |
| **Observabilité** | Logs, traces et métriques OpenTelemetry disponibles localement |
| **Migration de BD** | Schéma créé automatiquement avant le démarrage de la Function |

---

## Questions fréquentes

**Q : Est-ce que l'émulateur Service Bus est fidèle au vrai Azure Service Bus ?**
> Oui, l'image officielle `mcr.microsoft.com/azure-messaging/servicebus-emulator` reproduit le comportement réel, incluant le dead-lettering et les propriétés de message.

**Q : Comment passer en production ?**
> Il suffit de remplacer `.RunAsEmulator()` par une vraie ressource Azure Service Bus et de déployer avec `azd up`. Aspire gère la transition local → cloud.

**Q : Pourquoi Azurite est-il nécessaire ?**
> Azure Functions v4 requiert un compte de stockage (`AzureWebJobsStorage`) pour la coordination de l'hôte. Azurite l'émule localement. L'émulateur Service Bus l'utilise également pour son stockage interne.

**Q : Le schéma de la base est-il recréé à chaque démarrage ?**
> Non, le script SQL du DbMigrator est idempotent (`IF NOT EXISTS`). La table n'est créée que si elle n'existe pas déjà.