CREATE TABLE "Transactions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Transactions" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "Payee" TEXT NOT NULL,
    "Note" TEXT NOT NULL
);


CREATE TABLE "Users" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY,
    "OpenClawUserId" TEXT NOT NULL,
    "DefaultCurrency" TEXT NOT NULL
);


CREATE TABLE "Accounts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Accounts" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    CONSTRAINT "FK_Accounts_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Postings" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Postings" PRIMARY KEY,
    "TransactionId" TEXT NOT NULL,
    "AccountId" TEXT NOT NULL,
    "Amount" TEXT NOT NULL,
    "Currency" TEXT NOT NULL,
    CONSTRAINT "FK_Postings_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Postings_Transactions_TransactionId" FOREIGN KEY ("TransactionId") REFERENCES "Transactions" ("Id") ON DELETE CASCADE
);


CREATE INDEX "IX_Accounts_UserId" ON "Accounts" ("UserId");


CREATE INDEX "IX_Postings_AccountId" ON "Postings" ("AccountId");


CREATE INDEX "IX_Postings_TransactionId" ON "Postings" ("TransactionId");


