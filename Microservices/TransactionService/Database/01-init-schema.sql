-- Transaction Service Database Schema

SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0;
SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0;
SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';

-- -----------------------------------------------------
-- Schema transaction_db
-- -----------------------------------------------------
CREATE SCHEMA IF NOT EXISTS `transaction_db` DEFAULT CHARACTER SET utf8mb4;
USE `transaction_db`;

-- -----------------------------------------------------
-- Table `transaction_db`.`Transactions`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `transaction_db`.`Transactions` (
                                                               `Id` CHAR(36) NOT NULL COMMENT 'GUID as primary key',
    `TransferId` VARCHAR(100) NOT NULL COMMENT 'Public facing transaction ID',
    `FromAccount` VARCHAR(100) NOT NULL COMMENT 'Source account ID',
    `ToAccount` VARCHAR(100) NOT NULL COMMENT 'Destination account ID',
    `Amount` DECIMAL(18,2) NOT NULL COMMENT 'Transaction amount',
    `Status` VARCHAR(50) NOT NULL COMMENT 'Transaction status (pending, approved, declined)',
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Creation timestamp',
    `UpdatedAt` DATETIME NULL COMMENT 'Last update timestamp',
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_Transactions_TransferId` (`TransferId` ASC) VISIBLE,
    INDEX `IX_Transactions_FromAccount` (`FromAccount` ASC) VISIBLE,
    INDEX `IX_Transactions_ToAccount` (`ToAccount` ASC) VISIBLE,
    INDEX `IX_Transactions_Status` (`Status` ASC) VISIBLE,
    INDEX `IX_Transactions_CreatedAt` (`CreatedAt` ASC) VISIBLE
    ) ENGINE = InnoDB;

-- -----------------------------------------------------
-- Table `transaction_db`.`TransactionLogs`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `transaction_db`.`TransactionLogs` (
                                                                  `Id` CHAR(36) NOT NULL COMMENT 'GUID as primary key',
    `TransactionId` CHAR(36) NOT NULL COMMENT 'Reference to transaction ID',
    `LogType` VARCHAR(50) NOT NULL COMMENT 'Log type (status_change, fraud_check, error, etc.)',
    `Message` TEXT NOT NULL COMMENT 'Log message',
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Log timestamp',
    PRIMARY KEY (`Id`),
    INDEX `IX_TransactionLogs_TransactionId` (`TransactionId` ASC) VISIBLE,
    INDEX `IX_TransactionLogs_LogType` (`LogType` ASC) VISIBLE,
    INDEX `IX_TransactionLogs_CreatedAt` (`CreatedAt` ASC) VISIBLE,
    CONSTRAINT `FK_TransactionLogs_Transactions`
    FOREIGN KEY (`TransactionId`)
    REFERENCES `transaction_db`.`Transactions` (`Id`)
    ON DELETE CASCADE
    ON UPDATE NO ACTION
    ) ENGINE = InnoDB;

SET SQL_MODE=@OLD_SQL_MODE;
SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS;
SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS;
