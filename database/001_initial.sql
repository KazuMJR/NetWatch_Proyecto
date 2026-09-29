CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `DeviceTypes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Description` varchar(150) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_DeviceTypes` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Roles` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Description` varchar(200) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Roles` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Devices` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `DeviceTypeId` int NOT NULL,
    `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `IpAddress` varchar(45) CHARACTER SET utf8mb4 NOT NULL,
    `MacAddress` varchar(20) CHARACTER SET utf8mb4 NULL,
    `Manufacturer` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Model` varchar(100) CHARACTER SET utf8mb4 NULL,
    `OperatingSystem` varchar(100) CHARACTER SET utf8mb4 NULL,
    `Location` varchar(100) CHARACTER SET utf8mb4 NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NULL,
    `RegisteredAtUtc` datetime(6) NOT NULL,
    `LastPingAtUtc` datetime(6) NULL,
    `LastResponseTimeMs` decimal(8,2) NULL,
    `SubnetMask` varchar(45) CHARACTER SET utf8mb4 NOT NULL,
    `Gateway` varchar(45) CHARACTER SET utf8mb4 NULL,
    `PrimaryDns` varchar(45) CHARACTER SET utf8mb4 NULL,
    `SecondaryDns` varchar(45) CHARACTER SET utf8mb4 NULL,
    `IsActive` tinyint(1) NOT NULL,
    `MonitoringEnabled` tinyint(1) NOT NULL,
    `ConsecutivePingFailures` int NOT NULL,
    CONSTRAINT `PK_Devices` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Devices_DeviceTypes_DeviceTypeId` FOREIGN KEY (`DeviceTypeId`) REFERENCES `DeviceTypes` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `Users` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `RoleId` int NOT NULL,
    `FirstName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `LastName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Email` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
    `Username` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `PasswordHash` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `RegisteredAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_Users` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Users_Roles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `Alerts` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `DeviceId` int NOT NULL,
    `Title` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Level` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `GeneratedAtUtc` datetime(6) NOT NULL,
    `AttendedAtUtc` datetime(6) NULL,
    `AttendedByUserId` int NULL,
    CONSTRAINT `PK_Alerts` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Alerts_Devices_DeviceId` FOREIGN KEY (`DeviceId`) REFERENCES `Devices` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `DeviceStateHistory` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `DeviceId` int NOT NULL,
    `PreviousStatus` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `NewStatus` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NULL,
    `ChangedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_DeviceStateHistory` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DeviceStateHistory_Devices_DeviceId` FOREIGN KEY (`DeviceId`) REFERENCES `Devices` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `Events` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `DeviceId` int NOT NULL,
    `EventType` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Severity` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `OccurredAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_Events` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Events_Devices_DeviceId` FOREIGN KEY (`DeviceId`) REFERENCES `Devices` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `Metrics` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `DeviceId` int NOT NULL,
    `CpuPercent` decimal(5,2) NOT NULL,
    `MemoryPercent` decimal(5,2) NOT NULL,
    `DiskPercent` decimal(5,2) NOT NULL,
    `TemperatureCelsius` decimal(5,2) NULL,
    `NetworkTrafficMbps` decimal(10,2) NULL,
    `ResponseTimeMs` decimal(8,2) NOT NULL,
    `RegisteredAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_Metrics` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Metrics_Devices_DeviceId` FOREIGN KEY (`DeviceId`) REFERENCES `Devices` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_Alerts_DeviceId_Status_GeneratedAtUtc` ON `Alerts` (`DeviceId`, `Status`, `GeneratedAtUtc`);

CREATE INDEX `IX_Devices_DeviceTypeId` ON `Devices` (`DeviceTypeId`);

CREATE INDEX `IX_Devices_IpAddress` ON `Devices` (`IpAddress`);

CREATE INDEX `IX_DeviceStateHistory_DeviceId_ChangedAtUtc` ON `DeviceStateHistory` (`DeviceId`, `ChangedAtUtc`);

CREATE UNIQUE INDEX `IX_DeviceTypes_Name` ON `DeviceTypes` (`Name`);

CREATE INDEX `IX_Events_DeviceId_OccurredAtUtc` ON `Events` (`DeviceId`, `OccurredAtUtc`);

CREATE INDEX `IX_Metrics_DeviceId_RegisteredAtUtc` ON `Metrics` (`DeviceId`, `RegisteredAtUtc`);

CREATE UNIQUE INDEX `IX_Roles_Name` ON `Roles` (`Name`);

CREATE UNIQUE INDEX `IX_Users_Email` ON `Users` (`Email`);

CREATE INDEX `IX_Users_RoleId` ON `Users` (`RoleId`);

CREATE UNIQUE INDEX `IX_Users_Username` ON `Users` (`Username`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260923035256_InitialCreate', '8.0.13');

COMMIT;

