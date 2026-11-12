CREATE DATABASE IF NOT EXISTS `lgutreasury` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT ENCRYPTION='N';
USE `lgutreasury`;

DROP TABLE IF EXISTS `deletedrecords`;
DROP TABLE IF EXISTS `editrequests`;
DROP TABLE IF EXISTS `recordlineitems`;
DROP TABLE IF EXISTS `paymentrecords`;
DROP TABLE IF EXISTS `monthlybills`;
DROP TABLE IF EXISTS `accountbillingtypes`;
DROP TABLE IF EXISTS `revenuepolicies`;
DROP TABLE IF EXISTS `revenuetypes`;
DROP TABLE IF EXISTS `reportlog`;
DROP TABLE IF EXISTS `billingtypeoptions`;
DROP TABLE IF EXISTS `longtermpayees`;
DROP TABLE IF EXISTS `revenuecategories`;
DROP TABLE IF EXISTS `payees`;
DROP TABLE IF EXISTS `useraccounts`;
DROP TABLE IF EXISTS `__efmigrationshistory`;

CREATE TABLE `__efmigrationshistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `__efmigrationshistory` (`MigrationId`, `ProductVersion`) VALUES
  ('20260413070513_InitialCreate','10.0.5'),
  ('20260419170233_FixPayeeColumns','10.0.5'),
  ('20260503143244_AddPaymentMethodToPaymentRecord','10.0.5'),
  ('20260503155824_AddHasPendingRequestAndEditRequest','10.0.5'),
  ('20260507002503_addReportLog','10.0.5'),
  ('20260509153014_AddStatusToUserAccount','10.0.5'),
  ('20260509180223_AddProposedFieldsToEditReques','10.0.5'),
  ('20260510025818_AddIsCollectedToPaymentRecord','10.0.5'),
  ('20260516170601_RemoveProposedFields','10.0.5'),
  ('20260518111501_AddLongTermTenants','10.0.5'),
  ('20260528161058_AddDeletedRecords','10.0.5'),
  ('20260528161153_AddIsDeletedToPaymentRecord','10.0.5'),
  ('20260528162347_AddDeletedByNameToDeletedRecord','10.0.5'),
  ('20260528162709_AddDeletedByNameColumn','10.0.5'),
  ('20260531150447_AddEmailAndOtpToUserAccount','10.0.5'),
  ('20260930015719_AddPayorFullNameToPaymentRecord','10.0.9'),
  ('20261029100000_AddReportDownloadDetails','10.0.9'),
  ('20261106040205_InspectModelDrift','10.0.9'),
  ('20261109191500_AddProfilePictureToUserAccount','10.0.9');

CREATE TABLE `useraccounts` (
  `UserID` int NOT NULL AUTO_INCREMENT,
  `EmployeeID` longtext NOT NULL,
  `PasswordHash` longtext NOT NULL,
  `Role` longtext NOT NULL,
  `FirstName` longtext NOT NULL,
  `MiddleName` longtext,
  `LastName` longtext NOT NULL,
  `Suffix` longtext,
  `ContactNumber` longtext,
  `Address` longtext,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `Status` varchar(20) NOT NULL DEFAULT 'Pending',
  `Email` longtext,
  `OtpCode` longtext,
  `OtpExpiry` datetime(6) DEFAULT NULL,
  `ProfilePicture` longtext,
  PRIMARY KEY (`UserID`)
) ENGINE=InnoDB AUTO_INCREMENT=18 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `useraccounts` (`UserID`, `EmployeeID`, `PasswordHash`, `Role`, `FirstName`, `MiddleName`, `LastName`, `Suffix`, `ContactNumber`, `Address`, `IsActive`, `CreatedAt`, `Status`, `Email`, `OtpCode`, `OtpExpiry`, `ProfilePicture`) VALUES
  (1,'7240434','$2a$11$iLV.hFYYl7/qW3vUpHEHMunsbhhwfLmXj.ZnPB5h77gSG/Mij9Qaq','Admin','Gabriel',NULL,'Tolomea',NULL,'0987654321','Samboan,Cebu',1,'2026-04-14 14:06:04.714272','Active',NULL,NULL,NULL,NULL),
  (14,'654321','$2a$11$zszROWEa2hZLl/bHrKFq0OkRCSlPpjCzy/0noxpiRutkZOopiaFJ.','Collector','Leksha',NULL,'Lim',NULL,'09979376403','Poblacion,Samboan,Cebu,Philippines',1,'2026-06-12 00:31:08.233021','Active','Leksha@gmail.com',NULL,NULL,NULL),
  (15,'123456','$2a$11$cWswsAGTLwckCWwBpTaslO/72xeh6uhqN9UaLDbhqQ1Zmk9cBUqj2','Officer','Janice',NULL,'Chua',NULL,'09481747639','Malabuyoc,Cebu',1,'2026-06-12 00:32:23.597737','Active','Chua@gmail.com',NULL,NULL,NULL),
  (16,'841','$2a$11$Is6ucPtvDjE9eG4BdfLwmeqnW646mGfw8VFZdpAVcwz/csg2u3XPq','Admin','Anne Johanna','Oroc','Cadalin',NULL,NULL,'Palanas,Ginatilan,Cebu',1,'2026-09-03 14:14:08.682945','Active','mto.lgu.ginatilan@gmail.com',NULL,NULL,NULL),
  (17,'635','$2a$11$1hCP4Y3azvj5XEm/x4KoyOT.FrijCwXFWQBAfIc.aFQMwRG8CKv52','Admin','Geralyn','Eyas','Gosanes',NULL,NULL,'Mangaco, Ginatilan, Cebu',1,'2026-09-03 14:17:00.010064','Active','docxgera@gmail.com',NULL,NULL,NULL);

CREATE TABLE `payees` (
  `PayeeID` int NOT NULL AUTO_INCREMENT,
  `FirstName` longtext NOT NULL,
  `Middlename` longtext,
  `LastName` longtext NOT NULL,
  `Suffix` longtext,
  `ContactNumber` longtext,
  `ResidenceAddress` longtext,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`PayeeID`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `payees` (`PayeeID`, `FirstName`, `Middlename`, `LastName`, `Suffix`, `ContactNumber`, `ResidenceAddress`, `CreatedAt`) VALUES
  (1,'alex',NULL,'belongilot',NULL,NULL,NULL,'2026-10-19 14:14:33.930066'),
  (2,'Leksha',NULL,'Lim',NULL,NULL,NULL,'2026-10-19 15:21:29.721226'),
  (3,'kabutungan',NULL,'',NULL,NULL,NULL,'2026-10-19 15:35:02.706082'),
  (4,'Gabriel',NULL,'Tolomea',NULL,NULL,NULL,'2026-10-22 13:01:05.962446'),
  (5,'Janice Angel',NULL,'Chua',NULL,NULL,NULL,'2026-10-22 13:04:24.505889'),
  (6,'Water',NULL,'',NULL,NULL,NULL,'2026-10-23 11:46:01.120333');

CREATE TABLE `revenuecategories` (
  `CategoryID` varchar(255) NOT NULL,
  `Name` longtext NOT NULL,
  `Description` longtext,
  PRIMARY KEY (`CategoryID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `revenuecategories` (`CategoryID`, `Name`, `Description`) VALUES
  ('FEE','Fee',NULL),
  ('MARKET','Market',NULL),
  ('MISC','Miscellaneous',NULL),
  ('PERMIT','Permit',NULL),
  ('RENTAL','Rental',NULL),
  ('TAX','Tax',NULL),
  ('WATER','Water',NULL);

CREATE TABLE `longtermpayees` (
  `LongTermPayeeID` int NOT NULL AUTO_INCREMENT,
  `FirstName` longtext NOT NULL,
  `MiddleName` longtext,
  `LastName` longtext NOT NULL,
  `Suffix` longtext,
  `ContactNumber` longtext,
  `Address` longtext,
  `StartMonth` longtext NOT NULL,
  `BillGenerationDay` int NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `IsBillingEnabled` tinyint(1) NOT NULL DEFAULT '1',
  PRIMARY KEY (`LongTermPayeeID`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `longtermpayees` (`LongTermPayeeID`, `FirstName`, `MiddleName`, `LastName`, `Suffix`, `ContactNumber`, `Address`, `StartMonth`, `BillGenerationDay`, `IsActive`, `CreatedAt`, `IsBillingEnabled`) VALUES
  (1,'Melanie','Toco','Dinglasa',NULL,'09999999','Stall 2','2026-01',20,1,'2026-06-12 01:05:48.586554',1),
  (2,'Althea','','Barnes',NULL,'09656556','Stall no. 2','2026-02',20,1,'2026-06-12 01:14:58.948607',1),
  (3,'Alex','','Belongilot',NULL,'09453515','Sari Sari ni Alex','2025-11',20,1,'2026-06-12 01:17:01.154290',1),
  (4,'Gabriel','C','Tolomea',NULL,'','Stall 3','2026-02',20,1,'2026-10-11 23:31:02.359040',1),
  (5,'test','test','test',NULL,'0964656','test','2026-01',20,1,'2026-11-10 16:54:18.961335',1),
  (6,'stop','stop','stop',NULL,'09846941','stop','2026-01',20,1,'2026-11-10 16:55:10.271257',0);

CREATE TABLE `billingtypeoptions` (
  `BillingTypeOptionID` int NOT NULL AUTO_INCREMENT,
  `Name` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`BillingTypeOptionID`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `billingtypeoptions` (`BillingTypeOptionID`, `Name`, `CreatedAt`) VALUES
  (1,'Stall','2026-06-12 01:05:48.655718'),
  (2,'Sari Sari','2026-06-12 01:17:01.171258'),
  (3,'test','2026-11-10 16:54:19.017994'),
  (4,'stop','2026-11-10 16:55:10.278237');

CREATE TABLE `reportlog` (
  `LogID` int NOT NULL AUTO_INCREMENT,
  `ReportType` longtext NOT NULL,
  `Format` longtext NOT NULL,
  `GeneratedAt` datetime(6) NOT NULL,
  `GeneratedByUserID` int NOT NULL,
  `ReportKind` longtext,
  `FromDate` datetime(6) DEFAULT NULL,
  `ToDate` datetime(6) DEFAULT NULL,
  `RevenueTypeID` int DEFAULT NULL,
  `GeneratedByName` longtext,
  PRIMARY KEY (`LogID`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `reportlog` (`LogID`, `ReportType`, `Format`, `GeneratedAt`, `GeneratedByUserID`, `ReportKind`, `FromDate`, `ToDate`, `RevenueTypeID`, `GeneratedByName`) VALUES
  (1,'Monthly Revenue Summary','PDF','2026-10-29 22:49:37.707341',1,'monthly','2026-10-01 00:00:00.000000','2026-10-29 00:00:00.000000',NULL,'Gabriel Tolomea'),
  (2,'Monthly Revenue Summary','PDF','2026-11-04 10:09:47.055638',1,'monthly','2026-11-01 00:00:00.000000','2026-11-04 00:00:00.000000',NULL,'Gabriel Tolomea');

CREATE TABLE `revenuetypes` (
  `TypeID` int NOT NULL AUTO_INCREMENT,
  `CategoryID` varchar(255) NOT NULL,
  `Name` longtext NOT NULL,
  `BaseRate` decimal(10,2) NOT NULL,
  `IsRecurring` tinyint(1) NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `OrdinanceSection` longtext,
  `SortOrder` int NOT NULL DEFAULT '1000',
  `RowColor` varchar(7) DEFAULT NULL,
  PRIMARY KEY (`TypeID`),
  KEY `IX_RevenueTypes_CategoryID` (`CategoryID`),
  CONSTRAINT `FK_RevenueTypes_RevenueCategories_CategoryID` FOREIGN KEY (`CategoryID`) REFERENCES `revenuecategories` (`CategoryID`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=47 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `revenuetypes` (`TypeID`, `CategoryID`, `Name`, `BaseRate`, `IsRecurring`, `IsActive`, `OrdinanceSection`, `SortOrder`, `RowColor`) VALUES
  (2,'TAX','Mun. License & Business Tax',0.00,0,1,NULL,1000,NULL),
  (3,'FEE','Real Property Tax',0.00,0,1,NULL,1000,NULL),
  (4,'MARKET','Market Stall Fee',0.00,0,1,NULL,1000,NULL),
  (5,'FEE','Water Fees',0.00,0,1,NULL,1000,NULL),
  (6,'PERMIT','Building Permit Fee',0.00,0,1,NULL,1000,NULL),
  (7,'PERMIT','Burial',0.00,0,1,NULL,1000,NULL),
  (8,'FEE','CTC',0.00,0,1,NULL,1000,NULL),
  (13,'MISC','Police',0.00,0,1,NULL,1000,NULL),
  (14,'FEE','Rental',0.00,0,1,NULL,1000,NULL),
  (15,'PERMIT','Mayor\'s Permit Fees',0.00,0,1,NULL,1000,NULL),
  (17,'MISC','Inambakan',0.00,0,1,NULL,1000,NULL),
  (18,'MISC','Kabutongan',0.00,0,1,NULL,1000,NULL),
  (19,'MISC','Cock fight',0.00,0,1,NULL,1000,NULL),
  (20,'FEE','Garbage Fee',0.00,1,1,NULL,1000,NULL),
  (21,'TAX','Business Tax - Retailers',0.00,1,1,NULL,1000,NULL),
  (22,'TAX','Business Tax - Financial',0.00,1,1,NULL,1000,NULL),
  (23,'TAX','Business Tax - Others',0.00,1,1,NULL,1000,NULL),
  (24,'TAX','Peddlers Tax',0.00,0,1,NULL,1000,NULL),
  (25,'PERMIT','Police Clearance',0.00,0,1,NULL,1000,NULL),
  (26,'FEE','A.I. Fee',0.00,0,1,NULL,1000,NULL),
  (27,'PERMIT','Zoning',0.00,0,1,NULL,1000,NULL),
  (28,'FEE','Brgy./Sec.s Fee',0.00,0,1,NULL,1000,NULL),
  (29,'MISC','Miscellaneous',0.00,0,1,NULL,1000,NULL),
  (30,'FEE','Medical, Dental and Lab. Fee',0.00,0,1,NULL,1000,NULL),
  (31,'FEE','Fishery Rentals, Fees and Charges/Service Income',0.00,0,1,NULL,1000,NULL),
  (32,'FEE','Civil Registry Fees',0.00,0,1,NULL,1000,NULL),
  (33,'PERMIT','Other Clearances and Cert.',0.00,0,1,NULL,1000,NULL),
  (34,'FEE','Business Income/Parking Fee',0.00,0,1,NULL,1000,NULL),
  (35,'FEE','Occupational Fees',0.00,0,1,NULL,1000,NULL),
  (36,'FEE','Marriage',0.00,0,1,NULL,1000,NULL),
  (37,'FEE','Birth Certificate',0.00,0,1,NULL,1000,NULL),
  (38,'PERMIT','Cab/Bicycle Registration',0.00,0,1,NULL,1000,NULL),
  (39,'FEE','Weights and Measure',0.00,0,1,NULL,1000,NULL),
  (40,'MARKET','Market/Cash Tickets',0.00,0,1,NULL,1000,NULL),
  (41,'MARKET','Market/Rentals',0.00,1,1,NULL,1000,NULL),
  (42,'TAX','BIR Doc Stamp',0.00,0,1,NULL,1000,NULL),
  (43,'TAX','PTR',0.00,0,1,NULL,1000,NULL),
  (44,'FEE','Mining Fee',0.00,0,1,NULL,1000,NULL),
  (45,'PERMIT','Brgy. Clearance',0.00,0,1,NULL,1000,NULL),
  (46,'MISC','PCA',0.00,0,1,NULL,1000,NULL);

CREATE TABLE `revenuepolicies` (
  `PolicyID` int NOT NULL AUTO_INCREMENT,
  `TypeID` int NOT NULL,
  `SurchargeRate` decimal(5,2) NOT NULL,
  `InterestRate` decimal(5,2) NOT NULL,
  `Deadline` date DEFAULT NULL,
  `Notes` longtext,
  PRIMARY KEY (`PolicyID`),
  KEY `IX_RevenuePolicies_TypeID` (`TypeID`),
  CONSTRAINT `FK_RevenuePolicies_RevenueTypes_TypeID` FOREIGN KEY (`TypeID`) REFERENCES `revenuetypes` (`TypeID`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=21 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `revenuepolicies` (`PolicyID`, `TypeID`, `SurchargeRate`, `InterestRate`, `Deadline`, `Notes`) VALUES
  (2,2,0.00,0.00,NULL,NULL),
  (3,3,0.00,0.00,NULL,NULL),
  (4,4,0.00,0.00,NULL,NULL),
  (5,5,0.00,0.00,NULL,NULL),
  (6,6,0.00,0.00,NULL,NULL),
  (7,7,0.00,0.00,NULL,NULL),
  (8,8,0.00,0.00,NULL,NULL),
  (13,13,0.00,0.00,NULL,NULL),
  (14,14,0.00,0.00,NULL,NULL),
  (15,15,0.00,0.00,NULL,NULL),
  (17,17,0.00,0.00,NULL,NULL),
  (18,18,0.00,0.00,NULL,NULL),
  (19,19,0.00,0.00,NULL,NULL),
  (20,28,0.00,0.00,NULL,NULL);

CREATE TABLE `accountbillingtypes` (
  `AccountBillingTypeID` int NOT NULL AUTO_INCREMENT,
  `LongTermPayeeID` int NOT NULL,
  `BillingTypeName` longtext NOT NULL,
  `MonthlyRate` decimal(12,2) NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`AccountBillingTypeID`),
  KEY `IX_AccountBillingTypes_LongTermPayeeID` (`LongTermPayeeID`),
  CONSTRAINT `FK_AccountBillingTypes_LongTermPayees_LongTermPayeeID` FOREIGN KEY (`LongTermPayeeID`) REFERENCES `longtermpayees` (`LongTermPayeeID`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `accountbillingtypes` (`AccountBillingTypeID`, `LongTermPayeeID`, `BillingTypeName`, `MonthlyRate`, `IsActive`, `CreatedAt`) VALUES
  (1,1,'Stall',200.00,1,'2026-06-12 01:05:48.628973'),
  (2,2,'Stall',500.00,1,'2026-06-12 01:14:58.966743'),
  (3,3,'Sari Sari',600.00,1,'2026-06-12 01:17:01.169624'),
  (4,4,'Stall',600.00,1,'2026-10-11 23:31:02.542153'),
  (5,5,'test',500.00,1,'2026-11-10 16:54:19.007828'),
  (6,6,'stop',500.00,1,'2026-11-10 16:55:10.277416');

CREATE TABLE `monthlybills` (
  `MonthlyBillID` int NOT NULL AUTO_INCREMENT,
  `LongTermPayeeID` int NOT NULL,
  `AccountBillingTypeID` int NOT NULL,
  `BillingMonth` longtext,
  `BillingType` longtext,
  `BilledAmount` decimal(12,2) NOT NULL,
  `ORNumber` longtext,
  `Status` longtext NOT NULL,
  `PaidAt` datetime(6) DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`MonthlyBillID`),
  KEY `IX_MonthlyBills_AccountBillingTypeID` (`AccountBillingTypeID`),
  KEY `IX_MonthlyBills_LongTermPayeeID` (`LongTermPayeeID`),
  CONSTRAINT `FK_MonthlyBills_AccountBillingTypes_AccountBillingTypeID` FOREIGN KEY (`AccountBillingTypeID`) REFERENCES `accountbillingtypes` (`AccountBillingTypeID`) ON DELETE CASCADE,
  CONSTRAINT `FK_MonthlyBills_LongTermPayees_LongTermPayeeID` FOREIGN KEY (`LongTermPayeeID`) REFERENCES `longtermpayees` (`LongTermPayeeID`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=50 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `monthlybills` (`MonthlyBillID`, `LongTermPayeeID`, `AccountBillingTypeID`, `BillingMonth`, `BillingType`, `BilledAmount`, `ORNumber`, `Status`, `PaidAt`, `CreatedAt`) VALUES
  (1,1,1,'2026-01','Stall',200.00,'65487','Paid','2026-06-12 01:07:10.581844','2026-06-12 01:05:48.726756'),
  (2,1,1,'2026-02','Stall',200.00,'123450','Paid','2026-06-12 01:08:02.065135','2026-06-12 01:05:48.757849'),
  (3,1,1,'2026-03','Stall',200.00,NULL,'Unpaid',NULL,'2026-06-12 01:05:48.759923'),
  (4,1,1,'2026-04','Stall',200.00,NULL,'Unpaid',NULL,'2026-06-12 01:05:48.762093'),
  (5,1,1,'2026-05','Stall',200.00,NULL,'Unpaid',NULL,'2026-06-12 01:05:48.764333'),
  (6,1,1,'2026-06','Stall',200.00,NULL,'Unpaid',NULL,'2026-06-12 01:05:48.767414'),
  (7,1,1,'2025-12','Stall',200.00,'123452','Paid','2026-06-12 01:08:10.793471','2026-06-12 01:07:37.013652'),
  (8,2,2,'2026-02','Stall',500.00,'12323','Paid','2026-10-12 19:11:00.827089','2026-06-12 01:14:58.987492'),
  (9,2,2,'2026-03','Stall',500.00,'15656','Paid','2026-06-12 01:15:15.063840','2026-06-12 01:14:58.990800'),
  (10,2,2,'2026-04','Stall',500.00,'91452','Paid','2026-06-12 01:15:21.104398','2026-06-12 01:14:58.994572'),
  (11,2,2,'2026-05','Stall',500.00,'158954','Paid','2026-06-12 01:15:28.535781','2026-06-12 01:14:58.996913'),
  (12,2,2,'2026-06','Stall',500.00,NULL,'Unpaid',NULL,'2026-06-12 01:14:58.999506'),
  (13,3,3,'2025-11','Sari Sari',600.00,'98751','Paid','2026-06-12 01:17:19.365417','2026-06-12 01:17:01.186233'),
  (14,3,3,'2025-12','Sari Sari',600.00,'98752','Paid','2026-06-12 01:17:26.707096','2026-06-12 01:17:01.188041'),
  (15,3,3,'2026-01','Sari Sari',600.00,'98753','Paid','2026-06-12 01:17:33.836457','2026-06-12 01:17:01.189824'),
  (16,3,3,'2026-02','Sari Sari',600.00,'98754','Paid','2026-06-12 01:17:41.724527','2026-06-12 01:17:01.192791'),
  (17,3,3,'2026-03','Sari Sari',600.00,'98755','Paid','2026-06-12 01:17:48.462222','2026-06-12 01:17:01.195699'),
  (18,3,3,'2026-04','Sari Sari',600.00,'98756','Paid','2026-06-12 01:17:55.565608','2026-06-12 01:17:01.198756'),
  (19,3,3,'2026-05','Sari Sari',600.00,'98757','Paid','2026-06-12 01:18:02.741773','2026-06-12 01:17:01.200737'),
  (20,3,3,'2026-06','Sari Sari',600.00,NULL,'Unpaid',NULL,'2026-06-12 01:17:01.202884'),
  (21,4,4,'2026-02','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.616419'),
  (22,4,4,'2026-03','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.622771'),
  (23,4,4,'2026-04','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.625942'),
  (24,4,4,'2026-05','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.627742'),
  (25,4,4,'2026-06','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.629821'),
  (26,4,4,'2026-07','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.632307'),
  (27,4,4,'2026-08','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.634529'),
  (28,4,4,'2026-09','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.636292'),
  (29,4,4,'2026-10','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.637981'),
  (30,5,5,'2026-01','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.050045'),
  (31,5,5,'2026-02','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.054400'),
  (32,5,5,'2026-03','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.055452'),
  (33,5,5,'2026-04','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.056089'),
  (34,5,5,'2026-05','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.056751'),
  (35,5,5,'2026-06','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.057379'),
  (36,5,5,'2026-07','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.057996'),
  (37,5,5,'2026-08','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.058597'),
  (38,5,5,'2026-09','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.059203'),
  (39,5,5,'2026-10','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.059856'),
  (40,6,6,'2026-01','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.283459'),
  (41,6,6,'2026-02','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.284263'),
  (42,6,6,'2026-03','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.285071'),
  (43,6,6,'2026-04','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.285752'),
  (44,6,6,'2026-05','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.286399'),
  (45,6,6,'2026-06','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.286986'),
  (46,6,6,'2026-07','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.287546'),
  (47,6,6,'2026-08','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.288151'),
  (48,6,6,'2026-09','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.288770'),
  (49,6,6,'2026-10','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.289418');

CREATE TABLE `paymentrecords` (
  `PaymentID` int NOT NULL AUTO_INCREMENT,
  `OfficialReceipt` longtext NOT NULL,
  `PayeeID` int NOT NULL,
  `DateIssued` datetime(6) NOT NULL,
  `CollectedBy_UserID` int NOT NULL,
  `TotalBaseAmount` decimal(12,2) NOT NULL,
  `TotalSurcharge` decimal(12,2) NOT NULL,
  `TotalInterest` decimal(12,2) NOT NULL,
  `TotalAmount` decimal(12,2) NOT NULL,
  `Remarks` longtext,
  `CreatedAt` datetime(6) NOT NULL,
  `PaymentMethod` longtext,
  `HasPendingRequest` tinyint(1) NOT NULL DEFAULT '0',
  `CollectedConfirmedAt` datetime(6) DEFAULT NULL,
  `IsCollected` tinyint(1) NOT NULL DEFAULT '0',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `PayorFullName` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`PaymentID`),
  KEY `IX_PaymentRecords_CollectedBy_UserID` (`CollectedBy_UserID`),
  KEY `IX_PaymentRecords_PayeeID` (`PayeeID`),
  CONSTRAINT `FK_PaymentRecords_Payees_PayeeID` FOREIGN KEY (`PayeeID`) REFERENCES `payees` (`PayeeID`) ON DELETE CASCADE,
  CONSTRAINT `FK_PaymentRecords_UserAccounts_CollectedBy_UserID` FOREIGN KEY (`CollectedBy_UserID`) REFERENCES `useraccounts` (`UserID`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `paymentrecords` (`PaymentID`, `OfficialReceipt`, `PayeeID`, `DateIssued`, `CollectedBy_UserID`, `TotalBaseAmount`, `TotalSurcharge`, `TotalInterest`, `TotalAmount`, `Remarks`, `CreatedAt`, `PaymentMethod`, `HasPendingRequest`, `CollectedConfirmedAt`, `IsCollected`, `IsDeleted`, `PayorFullName`) VALUES
  (1,'00001',1,'2026-10-19 00:00:00.000000',14,800.00,0.00,0.00,800.00,NULL,'2026-10-19 14:14:34.337063',NULL,0,NULL,0,0,NULL),
  (2,'00002',2,'2026-10-19 00:00:00.000000',14,300.00,0.00,0.00,300.00,NULL,'2026-10-19 15:21:29.749437','Cash',0,NULL,0,0,NULL),
  (3,'255-300',3,'2026-10-19 00:00:00.000000',14,8000.00,0.00,0.00,8000.00,NULL,'2026-10-19 15:35:02.760741','Cash',1,NULL,0,0,NULL),
  (4,'254522',4,'2026-10-22 00:00:00.000000',14,599.80,0.00,0.00,599.80,NULL,'2026-10-22 13:01:06.263265',NULL,0,NULL,0,0,NULL),
  (5,'119119',5,'2026-10-22 00:00:00.000000',14,1200.00,0.34,0.00,1200.34,NULL,'2026-10-22 13:04:24.760622','Cash',0,NULL,0,0,NULL),
  (6,'50-60',6,'2026-10-23 00:00:00.000000',14,150.00,0.00,0.00,150.00,NULL,'2026-10-23 11:46:01.390830','Cash',0,NULL,0,1,NULL);

CREATE TABLE `recordlineitems` (
  `LineItemID` int NOT NULL AUTO_INCREMENT,
  `PaymentID` int NOT NULL,
  `TypeID` int NOT NULL,
  `Quantity` int NOT NULL,
  `BaseAmount` decimal(12,2) NOT NULL,
  `SurchargeAmount` decimal(12,2) NOT NULL,
  `InterestAmount` decimal(12,2) NOT NULL,
  `LineTotal` decimal(12,2) NOT NULL,
  PRIMARY KEY (`LineItemID`),
  KEY `IX_RecordLineItems_PaymentID` (`PaymentID`),
  KEY `IX_RecordLineItems_TypeID` (`TypeID`),
  CONSTRAINT `FK_RecordLineItems_PaymentRecords_PaymentID` FOREIGN KEY (`PaymentID`) REFERENCES `paymentrecords` (`PaymentID`) ON DELETE CASCADE,
  CONSTRAINT `FK_RecordLineItems_RevenueTypes_TypeID` FOREIGN KEY (`TypeID`) REFERENCES `revenuetypes` (`TypeID`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `recordlineitems` (`LineItemID`, `PaymentID`, `TypeID`, `Quantity`, `BaseAmount`, `SurchargeAmount`, `InterestAmount`, `LineTotal`) VALUES
  (1,1,15,1,800.00,0.00,0.00,800.00),
  (2,2,6,1,300.00,0.00,0.00,300.00),
  (3,3,18,46,8000.00,0.00,0.00,8000.00),
  (4,4,4,1,599.80,0.00,0.00,599.80),
  (5,5,3,1,1200.00,0.34,0.00,1200.34),
  (6,6,5,11,150.00,0.00,0.00,150.00);

CREATE TABLE `editrequests` (
  `RequestID` int NOT NULL AUTO_INCREMENT,
  `PaymentID` int NOT NULL,
  `RequestedBy_UserID` int NOT NULL,
  `Reason` longtext,
  `Status` varchar(20) NOT NULL DEFAULT 'Pending',
  `ReviewedBy_UserID` int DEFAULT NULL,
  `ReviewNote` longtext,
  `CreatedAt` datetime(6) NOT NULL,
  `ReviewedAt` datetime(6) DEFAULT NULL,
  `OfficialReceipt` longtext,
  PRIMARY KEY (`RequestID`),
  KEY `PaymentID` (`PaymentID`),
  KEY `RequestedBy_UserID` (`RequestedBy_UserID`),
  KEY `ReviewedBy_UserID` (`ReviewedBy_UserID`),
  CONSTRAINT `editrequests_ibfk_1` FOREIGN KEY (`PaymentID`) REFERENCES `paymentrecords` (`PaymentID`) ON DELETE CASCADE,
  CONSTRAINT `editrequests_ibfk_2` FOREIGN KEY (`RequestedBy_UserID`) REFERENCES `useraccounts` (`UserID`) ON DELETE CASCADE,
  CONSTRAINT `editrequests_ibfk_3` FOREIGN KEY (`ReviewedBy_UserID`) REFERENCES `useraccounts` (`UserID`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `editrequests` (`RequestID`, `PaymentID`, `RequestedBy_UserID`, `Reason`, `Status`, `ReviewedBy_UserID`, `ReviewNote`, `CreatedAt`, `ReviewedAt`, `OfficialReceipt`) VALUES
  (1,4,14,'wrong amount','Resolved',1,NULL,'2026-11-05 15:13:58.826145','2026-11-05 15:16:21.544513','254522'),
  (2,5,14,'change amount','Resolved',1,NULL,'2026-11-05 15:17:23.148023','2026-11-05 15:22:40.658626','119119'),
  (3,3,14,'test','Pending',NULL,NULL,'2026-11-05 15:43:07.944313',NULL,'255-300');

CREATE TABLE `deletedrecords` (
  `DeletedRecordID` int NOT NULL AUTO_INCREMENT,
  `PaymentID` int NOT NULL,
  `PaymentRecordPaymentID` int DEFAULT NULL,
  `PayeeName` longtext,
  `CollectorName` longtext,
  `CollectionType` longtext,
  `DeletedBy_UserID` int NOT NULL,
  `DeletedByUserID` int DEFAULT NULL,
  `DeletedAt` datetime(6) NOT NULL,
  `DeletedByName` longtext,
  `OfficialReceipt` longtext,
  PRIMARY KEY (`DeletedRecordID`),
  KEY `IX_DeletedRecords_DeletedByUserID` (`DeletedByUserID`),
  KEY `IX_DeletedRecords_PaymentRecordPaymentID` (`PaymentRecordPaymentID`),
  CONSTRAINT `FK_DeletedRecords_PaymentRecords_PaymentRecordPaymentID` FOREIGN KEY (`PaymentRecordPaymentID`) REFERENCES `paymentrecords` (`PaymentID`),
  CONSTRAINT `FK_DeletedRecords_UserAccounts_DeletedByUserID` FOREIGN KEY (`DeletedByUserID`) REFERENCES `useraccounts` (`UserID`)
) ENGINE=InnoDB AUTO_INCREMENT=19 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `deletedrecords` (`DeletedRecordID`, `PaymentID`, `PaymentRecordPaymentID`, `PayeeName`, `CollectorName`, `CollectionType`, `DeletedBy_UserID`, `DeletedByUserID`, `DeletedAt`, `DeletedByName`, `OfficialReceipt`) VALUES
  (18,6,NULL,', Water','Lim, Leksha','Water Fees',1,NULL,'2026-11-05 14:58:30.539855','Tolomea, Gabriel','50-60');
