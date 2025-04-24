-- Populate the Role table
INSERT INTO `useraccount_db`.`role` (`name`) VALUES ('user');
INSERT INTO `useraccount_db`.`role` (`name`) VALUES ('admin');

-- Populate the User table
-- Passwords are placeholder hashed values (in production, use bcrypt or similar)
INSERT INTO `useraccount_db`.`user` (`username`, `email`, `password`, `created_at`, `updated_at`, `Role_id`) 
VALUES ('john_doe', 'john.doe@example.com', '$2a$12$YrdZi3qtSPjpBaiIWiNojO42VUa8xQz9IEZYAP4l8qJ7ceD.c4OiK', NOW(), NOW(), 1); -- Role: user

INSERT INTO `useraccount_db`.`user` (`username`, `email`, `password`, `created_at`, `updated_at`, `Role_id`) 
VALUES ('jane_smith', 'jane.smith@example.com', '$2a$12$SC3DxilJquECG4.nUAYXFeVugHk10OtBe34eTJhAXPrmMVuVV/cZG', NOW(), NOW(), 1); -- Role: user

INSERT INTO `useraccount_db`.`user` (`username`, `email`, `password`, `created_at`, `updated_at`, `Role_id`) 
VALUES ('admin_user', 'admin@example.com', '$2a$12$04Kg/87QKMjpAoHlzRdTQuSHQgfwN6ouQVXY1jwOU9lnUw8OxbvrG', NOW(), NOW(), 2); -- Role: admin

-- Populate the Account table
-- John Doe's accounts
INSERT INTO `useraccount_db`.`account` (`name`, `amount`, `User_id`) 
VALUES ('Savings Account', 1000.50, 1);

INSERT INTO `useraccount_db`.`account` (`name`, `amount`, `User_id`) 
VALUES ('Checking Account', 500.25, 1);

-- Jane Smith's accounts
INSERT INTO `useraccount_db`.`account` (`name`, `amount`, `User_id`) 
VALUES ('Savings Account', 2000.75, 2);

-- Admin User's accounts
INSERT INTO `useraccount_db`.`account` (`name`, `amount`, `User_id`) 
VALUES ('Admin Account', 10000.00, 3);
