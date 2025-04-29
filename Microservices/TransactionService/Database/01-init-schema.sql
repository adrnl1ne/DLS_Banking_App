-- Transaction Service Database Schema - Simplified version with security enhancements

SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0;
SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0;

-- Schema creation
CREATE DATABASE IF NOT EXISTS `transaction_db`;
USE `transaction_db`;

-- Create Transactions table with all needed fields plus security enhancements
CREATE TABLE IF NOT EXISTS `Transactions` (
    `Id` VARCHAR(36) NOT NULL,
    `TransferId` VARCHAR(100) NOT NULL,
    `FromAccount` VARCHAR(100) NOT NULL,
    `ToAccount` VARCHAR(100) NOT NULL,
    `Amount` DECIMAL(18,2) NOT NULL,
    `Currency` VARCHAR(3) NOT NULL DEFAULT 'USD',
    `Status` VARCHAR(50) NOT NULL,
    `TransactionType` VARCHAR(50) NULL,
    `Description` VARCHAR(255) NULL,
    `UserId` INT NULL,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME NULL DEFAULT NULL COMMENT 'Last update timestamp',
    `LastModifiedBy` VARCHAR(100) NULL COMMENT 'User or service that last modified the transaction',
    `ClientIp` VARCHAR(45) NULL COMMENT 'IP address of client who initiated the transaction',
    `FraudCheckResult` TEXT NULL COMMENT 'Details from fraud detection (sanitized)',
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_Transactions_TransferId` (`TransferId`),
    INDEX `IX_Transactions_Status` (`Status`),
    INDEX `IX_Transactions_UserId` (`UserId`),
    INDEX `IX_Transactions_CreatedAt` (`CreatedAt`)
);

-- Create TransactionLogs table with security enhancements
CREATE TABLE IF NOT EXISTS `TransactionLogs` (
    `Id` VARCHAR(36) NOT NULL,
    `TransactionId` VARCHAR(36) NOT NULL,
    `LogType` VARCHAR(50) NOT NULL,
    `Message` TEXT NOT NULL,
    `SanitizedMessage` TEXT NULL COMMENT 'Sanitized version of the message',
    `ContainsSensitiveData` TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'Flag indicating if message contains sensitive data',
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`Id`),
    INDEX `IX_TransactionLogs_TransactionId` (`TransactionId`),
    INDEX `IX_TransactionLogs_SensitiveData` (`ContainsSensitiveData`),
    CONSTRAINT `FK_TransactionLogs_Transactions`
        FOREIGN KEY (`TransactionId`)
        REFERENCES `Transactions` (`Id`)
        ON DELETE CASCADE
);

SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS;
SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS;
