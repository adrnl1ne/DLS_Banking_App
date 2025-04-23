-- Populate the Role table
INSERT INTO `useraccount_db`.`Role` (`name`) VALUES ('user');
INSERT INTO `useraccount_db`.`Role` (`name`) VALUES ('admin');

-- Populate the User table
-- Passwords are placeholder hashed values (in production, use bcrypt or similar)
INSERT INTO `useraccount_db`.`User` (`username`, `email`, `password`, `created_at`, `updated_at`, `Role_id`) 
VALUES ('john_doe', 'john.doe@example.com', 'hashed_password_1', NOW(), NOW(), 1); -- Role: user

INSERT INTO `useraccount_db`.`User` (`username`, `email`, `password`, `created_at`, `updated_at`, `Role_id`) 
VALUES ('jane_smith', 'jane.smith@example.com', 'hashed_password_2', NOW(), NOW(), 1); -- Role: user

INSERT INTO `useraccount_db`.`User` (`username`, `email`, `password`, `created_at`, `updated_at`, `Role_id`) 
VALUES ('admin_user', 'admin@example.com', 'hashed_password_3', NOW(), NOW(), 2); -- Role: admin

-- Populate the Account table
-- John Doe's accounts
INSERT INTO `useraccount_db`.`Account` (`name`, `amount`, `User_id`) 
VALUES ('Savings Account', 1000.50, 1);

INSERT INTO `useraccount_db`.`Account` (`name`, `amount`, `User_id`) 
VALUES ('Checking Account', 500.25, 1);

-- Jane Smith's accounts
INSERT INTO `useraccount_db`.`Account` (`name`, `amount`, `User_id`) 
VALUES ('Savings Account', 2000.75, 2);

-- Admin User's accounts
INSERT INTO `useraccount_db`.`Account` (`name`, `amount`, `User_id`) 
VALUES ('Admin Account', 10000.00, 3);