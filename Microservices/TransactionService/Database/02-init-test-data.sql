-- Sample transaction data for testing

-- Insert sample transactions using actual account IDs from UserAccount service
INSERT INTO `transaction_db`.`Transactions`
(`Id`, `TransferId`, `FromAccount`, `ToAccount`, `Amount`, `Currency`, `Status`, `TransactionType`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`)
VALUES
(UUID(), 'TRX-001', '1', '2', 100.00, 'USD', 'approved', 'transfer', 'Transfer from savings to checking', 1, NOW(), NOW());

INSERT INTO `transaction_db`.`Transactions`
(`Id`, `TransferId`, `FromAccount`, `ToAccount`, `Amount`, `Currency`, `Status`, `TransactionType`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`)
VALUES
(UUID(), 'TRX-002', '2', '3', 50.00, 'USD', 'approved', 'transfer', 'Repayment to Jane', 1, NOW(), NOW());

INSERT INTO `transaction_db`.`Transactions`
(`Id`, `TransferId`, `FromAccount`, `ToAccount`, `Amount`, `Currency`, `Status`, `TransactionType`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`)
VALUES
(UUID(), 'TRX-003', '1', '3', 200.00, 'EUR', 'pending', 'payment', 'Invoice #12345', 1, NOW(), NOW());

INSERT INTO `transaction_db`.`Transactions`
(`Id`, `TransferId`, `FromAccount`, `ToAccount`, `Amount`, `Currency`, `Status`, `TransactionType`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`)
VALUES
(UUID(), 'TRX-004', '3', '4', 75.50, 'USD', 'declined', 'transfer', 'Suspicious activity', 2, NOW(), NOW());

-- Add another transaction showing transfer from admin to user
INSERT INTO `transaction_db`.`Transactions`
(`Id`, `TransferId`, `FromAccount`, `ToAccount`, `Amount`, `Currency`, `Status`, `TransactionType`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`)
VALUES
(UUID(), 'TRX-005', '4', '1', 500.00, 'USD', 'approved', 'transfer', 'Bonus payment', 3, NOW(), NOW());

-- Insert transaction logs for the transactions
-- Get the IDs of inserted transactions first
SET @trx1_id = (SELECT `Id` FROM `transaction_db`.`Transactions` WHERE `TransferId` = 'TRX-001');
SET @trx2_id = (SELECT `Id` FROM `transaction_db`.`Transactions` WHERE `TransferId` = 'TRX-002');
SET @trx3_id = (SELECT `Id` FROM `transaction_db`.`Transactions` WHERE `TransferId` = 'TRX-003');
SET @trx4_id = (SELECT `Id` FROM `transaction_db`.`Transactions` WHERE `TransferId` = 'TRX-004');
SET @trx5_id = (SELECT `Id` FROM `transaction_db`.`Transactions` WHERE `TransferId` = 'TRX-005');

-- Insert logs for each transaction
INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx1_id, 'status_change', 'Transaction created with status: pending', DATE_SUB(NOW(), INTERVAL 2 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx1_id, 'fraud_check', 'Fraud check passed', DATE_SUB(NOW(), INTERVAL 1 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx1_id, 'status_change', 'Transaction status updated to: approved', NOW());

-- Logs for transaction 2
INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx2_id, 'status_change', 'Transaction created with status: pending', DATE_SUB(NOW(), INTERVAL 3 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx2_id, 'fraud_check', 'Fraud check passed', DATE_SUB(NOW(), INTERVAL 2 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx2_id, 'status_change', 'Transaction status updated to: approved', DATE_SUB(NOW(), INTERVAL 2 HOUR));

-- Logs for transaction 3 (pending)
INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx3_id, 'status_change', 'Transaction created with status: pending', DATE_SUB(NOW(), INTERVAL 30 MINUTE));

-- Logs for transaction 4 (declined)
INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx4_id, 'status_change', 'Transaction created with status: pending', DATE_SUB(NOW(), INTERVAL 4 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx4_id, 'fraud_check', 'Possible fraud detected: unusual location', DATE_SUB(NOW(), INTERVAL 3 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx4_id, 'status_change', 'Transaction status updated to: declined', DATE_SUB(NOW(), INTERVAL 3 HOUR));

-- Logs for transaction 5 (bonus payment)
INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx5_id, 'status_change', 'Transaction created with status: pending', DATE_SUB(NOW(), INTERVAL 1 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx5_id, 'fraud_check', 'Fraud check passed: administrative action', DATE_SUB(NOW(), INTERVAL 45 MINUTE));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `CreatedAt`)
VALUES
(UUID(), @trx5_id, 'status_change', 'Transaction status updated to: approved', DATE_SUB(NOW(), INTERVAL 30 MINUTE));
