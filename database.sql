-- Optional: the app creates this automatically on first run.
-- Run this manually (MySQL Workbench) only if you prefer to set the database up yourself.
CREATE DATABASE IF NOT EXISTS cyberbot_db CHARACTER SET utf8mb4;
USE cyberbot_db;

CREATE TABLE IF NOT EXISTS Tasks (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Title VARCHAR(150) NOT NULL,
    Description VARCHAR(500) NOT NULL,
    ReminderDate DATETIME NULL,
    IsCompleted TINYINT(1) NOT NULL DEFAULT 0,
    ReminderNotified TINYINT(1) NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL
);
