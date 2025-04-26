-- Sample transaction data for testing with security measures

-- Insert sample transactions using actual account IDs from UserAccount service
INSERT INTO `transaction_db`.`Transactions`
(`Id`, `TransferId`, `FromAccount`, `ToAccount`, `Amount`, `Currency`, `Status`, `TransactionType`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`, `LastModifiedBy`, `ClientIp`, `FraudCheckResult`)
VALUES
(UUID(), 'TRX-001', '1', '2', 100.00, 'USD', 'approved', 'transfer', 'Transfer from savings to checking', 1, NOW(), NOW(), 'system', '127.0.0.1', 'No fraud detected');

INSERT INTO `transaction_db`.`Transactions`
(`Id`, `TransferId`, `FromAccount`, `ToAccount`, `Amount`, `Currency`, `Status`, `TransactionType`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`, `LastModifiedBy`, `ClientIp`, `FraudCheckResult`)
VALUES
(UUID(), 'TRX-002', '2', '3', 50.00, 'USD', 'approved', 'transfer', 'Repayment to Jane', 1, NOW(), NOW(), 'system', '127.0.0.1', 'No fraud detected');

INSERT INTO `transaction_db`.`Transactions`
(`Id`, `TransferId`, `FromAccount`, `ToAccount`, `Amount`, `Currency`, `Status`, `TransactionType`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`, `LastModifiedBy`, `ClientIp`, `FraudCheckResult`)
VALUES
(UUID(), 'TRX-003', '1', '3', 200.00, 'EUR', 'pending', 'payment', 'Invoice #12345', 1, NOW(), NOW(), 'system', '127.0.0.1', NULL);

INSERT INTO `transaction_db`.`Transactions`
(`Id`, `TransferId`, `FromAccount`, `ToAccount`, `Amount`, `Currency`, `Status`, `TransactionType`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`, `LastModifiedBy`, `ClientIp`, `FraudCheckResult`)
VALUES
(UUID(), 'TRX-004', '3', '4', 75.50, 'USD', 'declined', 'transfer', 'Suspicious activity', 2, NOW(), NOW(), 'system', '127.0.0.1', 'Unusual location detected');

-- Add another transaction showing transfer from admin to user
INSERT INTO `transaction_db`.`Transactions`
(`Id`, `TransferId`, `FromAccount`, `ToAccount`, `Amount`, `Currency`, `Status`, `TransactionType`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`, `LastModifiedBy`, `ClientIp`, `FraudCheckResult`)
VALUES
(UUID(), 'TRX-005', '4', '1', 500.00, 'USD', 'approved', 'transfer', 'Bonus payment', 3, NOW(), NOW(), 'admin', '127.0.0.1', 'Administrative action - approved');

-- Insert transaction logs for the transactions
-- Get the IDs of inserted transactions first
SET @trx1_id = (SELECT `Id` FROM `transaction_db`.`Transactions` WHERE `TransferId` = 'TRX-001');
SET @trx2_id = (SELECT `Id` FROM `transaction_db`.`Transactions` WHERE `TransferId` = 'TRX-002');
SET @trx3_id = (SELECT `Id` FROM `transaction_db`.`Transactions` WHERE `TransferId` = 'TRX-003');
SET @trx4_id = (SELECT `Id` FROM `transaction_db`.`Transactions` WHERE `TransferId` = 'TRX-004');
SET @trx5_id = (SELECT `Id` FROM `transaction_db`.`Transactions` WHERE `TransferId` = 'TRX-005');

-- Insert logs for each transaction with security fields
INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx1_id, 'status_change', 'Transaction created with status: pending', 'Transaction created with status: pending', 0, DATE_SUB(NOW(), INTERVAL 2 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx1_id, 'fraud_check', 'Fraud check passed for account 1 transferring $100.00 to account 2', 'Fraud check passed for account ** transferring ***.**** to account **', 1, DATE_SUB(NOW(), INTERVAL 1 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx1_id, 'status_change', 'Transaction status updated to: approved', 'Transaction status updated to: approved', 0, NOW());

-- Logs for transaction 2
INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx2_id, 'status_change', 'Transaction created with status: pending', 'Transaction created with status: pending', 0, DATE_SUB(NOW(), INTERVAL 3 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx2_id, 'fraud_check', 'Fraud check passed for account 2 transferring $50.00 to account 3', 'Fraud check passed for account ** transferring ***.**** to account **', 1, DATE_SUB(NOW(), INTERVAL 2 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx2_id, 'status_change', 'Transaction status updated to: approved', 'Transaction status updated to: approved', 0, DATE_SUB(NOW(), INTERVAL 2 HOUR));

-- Logs for transaction 3 (pending)
INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx3_id, 'status_change', 'Transaction created with status: pending for EUR 200.00', 'Transaction created with status: pending for EUR ***.**', 1, DATE_SUB(NOW(), INTERVAL 30 MINUTE));

-- Logs for transaction 4 (declined)
INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx4_id, 'status_change', 'Transaction created with status: pending', 'Transaction created with status: pending', 0, DATE_SUB(NOW(), INTERVAL 4 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx4_id, 'fraud_check', 'Possible fraud detected: unusual location for account 3 transferring $75.50 to account 4', 'Possible fraud detected: unusual location for account ** transferring ***.**** to account **', 1, DATE_SUB(NOW(), INTERVAL 3 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx4_id, 'status_change', 'Transaction status updated to: declined', 'Transaction status updated to: declined', 0, DATE_SUB(NOW(), INTERVAL 3 HOUR));

-- Logs for transaction 5 (bonus payment)
INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx5_id, 'status_change', 'Transaction created with status: pending', 'Transaction created with status: pending', 0, DATE_SUB(NOW(), INTERVAL 1 HOUR));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx5_id, 'fraud_check', 'Fraud check passed: administrative action for $500.00 transfer from account 4 to account 1', 'Fraud check passed: administrative action for ***.**** transfer from account ** to account **', 1, DATE_SUB(NOW(), INTERVAL 45 MINUTE));

INSERT INTO `transaction_db`.`TransactionLogs`
(`Id`, `TransactionId`, `LogType`, `Message`, `SanitizedMessage`, `ContainsSensitiveData`, `CreatedAt`)
VALUES
(UUID(), @trx5_id, 'status_change', 'Transaction status updated to: approved', 'Transaction status updated to: approved', 0, DATE_SUB(NOW(), INTERVAL 30 MINUTE));
