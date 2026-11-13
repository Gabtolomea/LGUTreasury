-- =====================================================================
-- LGUTreasury Consolidated Data Import Script
-- Source: Dump-2026-11-10
-- Generated for importing and synchronizing data into an existing database
-- =====================================================================

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

-- ---------------------------------------------------------------------
-- Table: `__EFMigrationsHistory`
-- ---------------------------------------------------------------------
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES
('20260413070513_InitialCreate','10.0.5'),('20260419170233_FixPayeeColumns','10.0.5'),('20260503143244_AddPaymentMethodToPaymentRecord','10.0.5'),('20260503155824_AddHasPendingRequestAndEditRequest','10.0.5'),('20260507002503_addReportLog','10.0.5'),('20260509153014_AddStatusToUserAccount','10.0.5'),('20260509180223_AddProposedFieldsToEditReques','10.0.5'),('20260510025818_AddIsCollectedToPaymentRecord','10.0.5'),('20260516170601_RemoveProposedFields','10.0.5'),('20260518111501_AddLongTermTenants','10.0.5'),('20260528161058_AddDeletedRecords','10.0.5'),('20260528161153_AddIsDeletedToPaymentRecord','10.0.5'),('20260528162347_AddDeletedByNameToDeletedRecord','10.0.5'),('20260528162709_AddDeletedByNameColumn','10.0.5'),('20260531150447_AddEmailAndOtpToUserAccount','10.0.5'),('20260930015719_AddPayorFullNameToPaymentRecord','10.0.9'),('20261029100000_AddReportDownloadDetails','10.0.9'),('20261106040205_InspectModelDrift','10.0.9'),('20261109191500_AddProfilePictureToUserAccount','10.0.9')
 ON DUPLICATE KEY UPDATE
  `ProductVersion` = VALUES(`ProductVersion`);

-- ---------------------------------------------------------------------
-- Table: `RevenueCategories`
-- ---------------------------------------------------------------------
INSERT INTO `RevenueCategories` (`CategoryID`, `Name`, `Description`) VALUES
('FEE','Fee',NULL),('MARKET','Market',NULL),('MISC','Miscellaneous',NULL),('PERMIT','Permit',NULL),('RENTAL','Rental',NULL),('TAX','Tax',NULL),('WATER','Water',NULL)
 ON DUPLICATE KEY UPDATE
  `Name` = VALUES(`Name`),
  `Description` = VALUES(`Description`);

-- ---------------------------------------------------------------------
-- Table: `RevenueTypes`
-- ---------------------------------------------------------------------
INSERT INTO `RevenueTypes` (`TypeID`, `CategoryID`, `Name`, `BaseRate`, `IsRecurring`, `IsActive`, `OrdinanceSection`) VALUES
(2,'TAX','Mun. License & Business Tax',0.00,0,1,NULL),
(3,'FEE','Real Property Tax',0.00,0,1,NULL),
(4,'MARKET','Market Stall Fee',0.00,0,1,NULL),
(5,'FEE','Water Fees',0.00,0,1,NULL),
(6,'PERMIT','Building Permit Fee',0.00,0,1,NULL),
(7,'PERMIT','Burial',0.00,0,1,NULL),
(8,'FEE','CTC',0.00,0,1,NULL),
(13,'MISC','Police',0.00,0,1,NULL),
(14,'FEE','Rental',0.00,0,1,NULL),
(15,'PERMIT','Mayor\'s Permit Fees',0.00,0,1,NULL),
(17,'MISC','Inambakan',0.00,0,1,NULL),
(18,'MISC','Kabutongan',0.00,0,1,NULL),
(19,'MISC','Cock fight',0.00,0,1,NULL),
(20,'FEE','Garbage Fee',0.00,1,1,NULL),
(21,'TAX','Business Tax - Retailers',0.00,1,1,NULL),
(22,'TAX','Business Tax - Financial',0.00,1,1,NULL),
(23,'TAX','Business Tax - Others',0.00,1,1,NULL),
(24,'TAX','Peddlers Tax',0.00,0,1,NULL),
(25,'PERMIT','Police Clearance',0.00,0,1,NULL),
(26,'FEE','A.I. Fee',0.00,0,1,NULL),
(27,'PERMIT','Zoning',0.00,0,1,NULL),
(28,'FEE','Brgy./Sec.s Fee',0.00,0,1,NULL),
(29,'MISC','Miscellaneous',0.00,0,1,NULL),
(30,'FEE','Medical, Dental and Lab. Fee',0.00,0,1,NULL),
(31,'FEE','Fishery Rentals, Fees and Charges/Service Income',0.00,0,1,NULL),
(32,'FEE','Civil Registry Fees',0.00,0,1,NULL),
(33,'PERMIT','Other Clearances and Cert.',0.00,0,1,NULL),
(34,'FEE','Business Income/Parking Fee',0.00,0,1,NULL),
(35,'FEE','Occupational Fees',0.00,0,1,NULL),
(36,'FEE','Marriage',0.00,0,1,NULL),
(37,'FEE','Birth Certificate',0.00,0,1,NULL),
(38,'PERMIT','Cab/Bicycle Registration',0.00,0,1,NULL),
(39,'FEE','Weights and Measure',0.00,0,1,NULL),
(40,'MARKET','Market/Cash Tickets',0.00,0,1,NULL),
(41,'MARKET','Market/Rentals',0.00,1,1,NULL),
(42,'TAX','BIR Doc Stamp',0.00,0,1,NULL),
(43,'TAX','PTR',0.00,0,1,NULL),
(44,'FEE','Mining Fee',0.00,0,1,NULL),
(45,'PERMIT','Brgy. Clearance',0.00,0,1,NULL),
(46,'MISC','PCA',0.00,0,1,NULL)
 ON DUPLICATE KEY UPDATE
  `CategoryID` = VALUES(`CategoryID`),
  `Name` = VALUES(`Name`),
  `BaseRate` = VALUES(`BaseRate`),
  `IsRecurring` = VALUES(`IsRecurring`),
  `IsActive` = VALUES(`IsActive`),
  `OrdinanceSection` = VALUES(`OrdinanceSection`);
ALTER TABLE `RevenueTypes` AUTO_INCREMENT = 47;

-- ---------------------------------------------------------------------
-- Table: `RevenuePolicies`
-- ---------------------------------------------------------------------
INSERT INTO `RevenuePolicies` (`PolicyID`, `TypeID`, `SurchargeRate`, `InterestRate`, `Deadline`, `Notes`) VALUES
(2,2,0.00,0.00,NULL,NULL),(3,3,0.00,0.00,NULL,NULL),(4,4,0.00,0.00,NULL,NULL),(5,5,0.00,0.00,NULL,NULL),(6,6,0.00,0.00,NULL,NULL),(7,7,0.00,0.00,NULL,NULL),(8,8,0.00,0.00,NULL,NULL),(13,13,0.00,0.00,NULL,NULL),(14,14,0.00,0.00,NULL,NULL),(15,15,0.00,0.00,NULL,NULL),(17,17,0.00,0.00,NULL,NULL),(18,18,0.00,0.00,NULL,NULL),(19,19,0.00,0.00,NULL,NULL),(20,28,0.00,0.00,NULL,NULL)
 ON DUPLICATE KEY UPDATE
  `TypeID` = VALUES(`TypeID`),
  `SurchargeRate` = VALUES(`SurchargeRate`),
  `InterestRate` = VALUES(`InterestRate`),
  `Deadline` = VALUES(`Deadline`),
  `Notes` = VALUES(`Notes`);
ALTER TABLE `RevenuePolicies` AUTO_INCREMENT = 21;

-- ---------------------------------------------------------------------
-- Table: `BillingTypeOptions`
-- ---------------------------------------------------------------------
INSERT INTO `BillingTypeOptions` (`BillingTypeOptionID`, `Name`, `CreatedAt`) VALUES
(1,'Stall','2026-06-12 01:05:48.655718'),(2,'Sari Sari','2026-06-12 01:17:01.171258'),(3,'test','2026-11-10 16:54:19.017994'),(4,'stop','2026-11-10 16:55:10.278237')
 ON DUPLICATE KEY UPDATE
  `Name` = VALUES(`Name`),
  `CreatedAt` = VALUES(`CreatedAt`);
ALTER TABLE `BillingTypeOptions` AUTO_INCREMENT = 5;

-- ---------------------------------------------------------------------
-- Table: `UserAccounts`
-- ---------------------------------------------------------------------
INSERT INTO `UserAccounts` (`UserID`, `EmployeeID`, `PasswordHash`, `Role`, `FirstName`, `MiddleName`, `LastName`, `Suffix`, `ContactNumber`, `Address`, `IsActive`, `CreatedAt`, `Status`, `Email`, `OtpCode`, `OtpExpiry`, `ProfilePicture`) VALUES
(1,'7240434','$2a$11$iLV.hFYYl7/qW3vUpHEHMunsbhhwfLmXj.ZnPB5h77gSG/Mij9Qaq','Admin','Gabriel',NULL,'Tolomea',NULL,'0987654321','Samboan,Cebu',1,'2026-04-14 14:06:04.714272','Active',NULL,NULL,NULL,NULL),(14,'654321','$2a$11$zszROWEa2hZLl/bHrKFq0OkRCSlPpjCzy/0noxpiRutkZOopiaFJ.','Collector','Leksha',NULL,'Lim',NULL,'09979376403','Poblacion,Samboan,Cebu,Philippines',1,'2026-06-12 00:31:08.233021','Active','Leksha@gmail.com',NULL,NULL,NULL),(15,'123456','$2a$11$cWswsAGTLwckCWwBpTaslO/72xeh6uhqN9UaLDbhqQ1Zmk9cBUqj2','Officer','Janice',NULL,'Chua',NULL,'09481747639','Malabuyoc,Cebu',1,'2026-06-12 00:32:23.597737','Active','Chua@gmail.com',NULL,NULL,NULL),(16,'841','$2a$11$Is6ucPtvDjE9eG4BdfLwmeqnW646mGfw8VFZdpAVcwz/csg2u3XPq','Admin','Anne Johanna','Oroc','Cadalin',NULL,NULL,'Palanas,Ginatilan,Cebu',1,'2026-09-03 14:14:08.682945','Active','mto.lgu.ginatilan@gmail.com',NULL,NULL,NULL),(17,'635','$2a$11$1hCP4Y3azvj5XEm/x4KoyOT.FrijCwXFWQBAfIc.aFQMwRG8CKv52','Admin','Geralyn','Eyas','Gosanes',NULL,NULL,'Mangaco, Ginatilan, Cebu',1,'2026-09-03 14:17:00.010064','Active','docxgera@gmail.com',NULL,NULL,NULL)
 ON DUPLICATE KEY UPDATE
  `EmployeeID` = VALUES(`EmployeeID`),
  `PasswordHash` = VALUES(`PasswordHash`),
  `Role` = VALUES(`Role`),
  `FirstName` = VALUES(`FirstName`),
  `MiddleName` = VALUES(`MiddleName`),
  `LastName` = VALUES(`LastName`),
  `Suffix` = VALUES(`Suffix`),
  `ContactNumber` = VALUES(`ContactNumber`),
  `Address` = VALUES(`Address`),
  `IsActive` = VALUES(`IsActive`),
  `CreatedAt` = VALUES(`CreatedAt`),
  `Status` = VALUES(`Status`),
  `Email` = VALUES(`Email`),
  `OtpCode` = VALUES(`OtpCode`),
  `OtpExpiry` = VALUES(`OtpExpiry`),
  `ProfilePicture` = VALUES(`ProfilePicture`);
ALTER TABLE `UserAccounts` AUTO_INCREMENT = 18;

-- ---------------------------------------------------------------------
-- Table: `LongTermPayees`
-- ---------------------------------------------------------------------
INSERT INTO `LongTermPayees` (`LongTermPayeeID`, `FirstName`, `MiddleName`, `LastName`, `Suffix`, `ContactNumber`, `Address`, `StartMonth`, `BillGenerationDay`, `IsActive`, `CreatedAt`, `IsBillingEnabled`) VALUES
(1,'Melanie','Toco','Dinglasa',NULL,'09999999','Stall 2','2026-01',20,1,'2026-06-12 01:05:48.586554',1),(2,'Althea','','Barnes',NULL,'09656556','Stall no. 2','2026-02',20,1,'2026-06-12 01:14:58.948607',1),(3,'Alex','','Belongilot',NULL,'09453515','Sari Sari ni Alex','2025-11',20,1,'2026-06-12 01:17:01.154290',1),(4,'Gabriel','C','Tolomea',NULL,'','Stall 3','2026-02',20,1,'2026-10-11 23:31:02.359040',1),(5,'test','test','test',NULL,'0964656','test','2026-01',20,1,'2026-11-10 16:54:18.961335',1),(6,'stop','stop','stop',NULL,'09846941','stop','2026-01',20,1,'2026-11-10 16:55:10.271257',0)
 ON DUPLICATE KEY UPDATE
  `FirstName` = VALUES(`FirstName`),
  `MiddleName` = VALUES(`MiddleName`),
  `LastName` = VALUES(`LastName`),
  `Suffix` = VALUES(`Suffix`),
  `ContactNumber` = VALUES(`ContactNumber`),
  `Address` = VALUES(`Address`),
  `StartMonth` = VALUES(`StartMonth`),
  `BillGenerationDay` = VALUES(`BillGenerationDay`),
  `IsActive` = VALUES(`IsActive`),
  `CreatedAt` = VALUES(`CreatedAt`),
  `IsBillingEnabled` = VALUES(`IsBillingEnabled`);
ALTER TABLE `LongTermPayees` AUTO_INCREMENT = 7;

-- ---------------------------------------------------------------------
-- Table: `AccountBillingTypes`
-- ---------------------------------------------------------------------
INSERT INTO `AccountBillingTypes` (`AccountBillingTypeID`, `LongTermPayeeID`, `BillingTypeName`, `MonthlyRate`, `IsActive`, `CreatedAt`) VALUES
(1,1,'Stall',200.00,1,'2026-06-12 01:05:48.628973'),(2,2,'Stall',500.00,1,'2026-06-12 01:14:58.966743'),(3,3,'Sari Sari',600.00,1,'2026-06-12 01:17:01.169624'),(4,4,'Stall',600.00,1,'2026-10-11 23:31:02.542153'),(5,5,'test',500.00,1,'2026-11-10 16:54:19.007828'),(6,6,'stop',500.00,1,'2026-11-10 16:55:10.277416')
 ON DUPLICATE KEY UPDATE
  `LongTermPayeeID` = VALUES(`LongTermPayeeID`),
  `BillingTypeName` = VALUES(`BillingTypeName`),
  `MonthlyRate` = VALUES(`MonthlyRate`),
  `IsActive` = VALUES(`IsActive`),
  `CreatedAt` = VALUES(`CreatedAt`);
ALTER TABLE `AccountBillingTypes` AUTO_INCREMENT = 7;

-- ---------------------------------------------------------------------
-- Table: `MonthlyBills`
-- ---------------------------------------------------------------------
INSERT INTO `MonthlyBills` (`MonthlyBillID`, `LongTermPayeeID`, `AccountBillingTypeID`, `BillingMonth`, `BillingType`, `BilledAmount`, `ORNumber`, `Status`, `PaidAt`, `CreatedAt`) VALUES
(1,1,1,'2026-01','Stall',200.00,'65487','Paid','2026-06-12 01:07:10.581844','2026-06-12 01:05:48.726756'),(2,1,1,'2026-02','Stall',200.00,'123450','Paid','2026-06-12 01:08:02.065135','2026-06-12 01:05:48.757849'),(3,1,1,'2026-03','Stall',200.00,NULL,'Unpaid',NULL,'2026-06-12 01:05:48.759923'),(4,1,1,'2026-04','Stall',200.00,NULL,'Unpaid',NULL,'2026-06-12 01:05:48.762093'),(5,1,1,'2026-05','Stall',200.00,NULL,'Unpaid',NULL,'2026-06-12 01:05:48.764333'),(6,1,1,'2026-06','Stall',200.00,NULL,'Unpaid',NULL,'2026-06-12 01:05:48.767414'),(7,1,1,'2025-12','Stall',200.00,'123452','Paid','2026-06-12 01:08:10.793471','2026-06-12 01:07:37.013652'),(8,2,2,'2026-02','Stall',500.00,'12323','Paid','2026-10-12 19:11:00.827089','2026-06-12 01:14:58.987492'),(9,2,2,'2026-03','Stall',500.00,'15656','Paid','2026-06-12 01:15:15.063840','2026-06-12 01:14:58.990800'),(10,2,2,'2026-04','Stall',500.00,'91452','Paid','2026-06-12 01:15:21.104398','2026-06-12 01:14:58.994572'),(11,2,2,'2026-05','Stall',500.00,'158954','Paid','2026-06-12 01:15:28.535781','2026-06-12 01:14:58.996913'),(12,2,2,'2026-06','Stall',500.00,NULL,'Unpaid',NULL,'2026-06-12 01:14:58.999506'),(13,3,3,'2025-11','Sari Sari',600.00,'98751','Paid','2026-06-12 01:17:19.365417','2026-06-12 01:17:01.186233'),(14,3,3,'2025-12','Sari Sari',600.00,'98752','Paid','2026-06-12 01:17:26.707096','2026-06-12 01:17:01.188041'),(15,3,3,'2026-01','Sari Sari',600.00,'98753','Paid','2026-06-12 01:17:33.836457','2026-06-12 01:17:01.189824'),(16,3,3,'2026-02','Sari Sari',600.00,'98754','Paid','2026-06-12 01:17:41.724527','2026-06-12 01:17:01.192791'),(17,3,3,'2026-03','Sari Sari',600.00,'98755','Paid','2026-06-12 01:17:48.462222','2026-06-12 01:17:01.195699'),(18,3,3,'2026-04','Sari Sari',600.00,'98756','Paid','2026-06-12 01:17:55.565608','2026-06-12 01:17:01.198756'),(19,3,3,'2026-05','Sari Sari',600.00,'98757','Paid','2026-06-12 01:18:02.741773','2026-06-12 01:17:01.200737'),(20,3,3,'2026-06','Sari Sari',600.00,NULL,'Unpaid',NULL,'2026-06-12 01:17:01.202884'),(21,4,4,'2026-02','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.616419'),(22,4,4,'2026-03','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.622771'),(23,4,4,'2026-04','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.625942'),(24,4,4,'2026-05','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.627742'),(25,4,4,'2026-06','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.629821'),(26,4,4,'2026-07','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.632307'),(27,4,4,'2026-08','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.634529'),(28,4,4,'2026-09','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.636292'),(29,4,4,'2026-10','Stall',600.00,NULL,'Unpaid',NULL,'2026-10-11 23:31:02.637981'),(30,5,5,'2026-01','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.050045'),(31,5,5,'2026-02','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.054400'),(32,5,5,'2026-03','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.055452'),(33,5,5,'2026-04','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.056089'),(34,5,5,'2026-05','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.056751'),(35,5,5,'2026-06','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.057379'),(36,5,5,'2026-07','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.057996'),(37,5,5,'2026-08','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.058597'),(38,5,5,'2026-09','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.059203'),(39,5,5,'2026-10','test',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:54:19.059856'),(40,6,6,'2026-01','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.283459'),(41,6,6,'2026-02','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.284263'),(42,6,6,'2026-03','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.285071'),(43,6,6,'2026-04','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.285752'),(44,6,6,'2026-05','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.286399'),(45,6,6,'2026-06','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.286986'),(46,6,6,'2026-07','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.287546'),(47,6,6,'2026-08','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.288151'),(48,6,6,'2026-09','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.288770'),(49,6,6,'2026-10','stop',500.00,NULL,'Unpaid',NULL,'2026-11-10 16:55:10.289418')
 ON DUPLICATE KEY UPDATE
  `LongTermPayeeID` = VALUES(`LongTermPayeeID`),
  `AccountBillingTypeID` = VALUES(`AccountBillingTypeID`),
  `BillingMonth` = VALUES(`BillingMonth`),
  `BillingType` = VALUES(`BillingType`),
  `BilledAmount` = VALUES(`BilledAmount`),
  `ORNumber` = VALUES(`ORNumber`),
  `Status` = VALUES(`Status`),
  `PaidAt` = VALUES(`PaidAt`),
  `CreatedAt` = VALUES(`CreatedAt`);
ALTER TABLE `MonthlyBills` AUTO_INCREMENT = 50;

-- ---------------------------------------------------------------------
-- Table: `Payees`
-- ---------------------------------------------------------------------
INSERT INTO `Payees` (`PayeeID`, `Firstname`, `Middlename`, `Lastname`, `Suffix`, `ContactNumber`, `ResidenceAddress`, `CreatedAt`) VALUES
(1,'alex',NULL,'belongilot',NULL,NULL,NULL,'2026-10-19 14:14:33.930066'),(2,'Leksha',NULL,'Lim',NULL,NULL,NULL,'2026-10-19 15:21:29.721226'),(3,'kabutungan',NULL,'',NULL,NULL,NULL,'2026-10-19 15:35:02.706082'),(4,'Gabriel',NULL,'Tolomea',NULL,NULL,NULL,'2026-10-22 13:01:05.962446'),(5,'Janice Angel',NULL,'Chua',NULL,NULL,NULL,'2026-10-22 13:04:24.505889'),(6,'Water',NULL,'',NULL,NULL,NULL,'2026-10-23 11:46:01.120333')
 ON DUPLICATE KEY UPDATE
  `Firstname` = VALUES(`Firstname`),
  `Middlename` = VALUES(`Middlename`),
  `Lastname` = VALUES(`Lastname`),
  `Suffix` = VALUES(`Suffix`),
  `ContactNumber` = VALUES(`ContactNumber`),
  `ResidenceAddress` = VALUES(`ResidenceAddress`),
  `CreatedAt` = VALUES(`CreatedAt`);
ALTER TABLE `Payees` AUTO_INCREMENT = 7;

-- ---------------------------------------------------------------------
-- Table: `PaymentRecords`
-- ---------------------------------------------------------------------
INSERT INTO `PaymentRecords` (`PaymentID`, `OfficialReceipt`, `PayeeID`, `DateIssued`, `CollectedBy_UserID`, `TotalBaseAmount`, `TotalSurcharge`, `TotalInterest`, `TotalAmount`, `Remarks`, `CreatedAt`, `PaymentMethod`, `HasPendingRequest`, `CollectedConfirmedAt`, `IsCollected`, `IsDeleted`, `PayorFullName`) VALUES
(1,'00001',1,'2026-10-19 00:00:00.000000',14,800.00,0.00,0.00,800.00,NULL,'2026-10-19 14:14:34.337063',NULL,0,NULL,0,0,NULL),(2,'00002',2,'2026-10-19 00:00:00.000000',14,300.00,0.00,0.00,300.00,NULL,'2026-10-19 15:21:29.749437','Cash',0,NULL,0,0,NULL),(3,'255-300',3,'2026-10-19 00:00:00.000000',14,8000.00,0.00,0.00,8000.00,NULL,'2026-10-19 15:35:02.760741','Cash',1,NULL,0,0,NULL),(4,'254522',4,'2026-10-22 00:00:00.000000',14,599.80,0.00,0.00,599.80,NULL,'2026-10-22 13:01:06.263265',NULL,0,NULL,0,0,NULL),(5,'119119',5,'2026-10-22 00:00:00.000000',14,1200.00,0.34,0.00,1200.34,NULL,'2026-10-22 13:04:24.760622','Cash',0,NULL,0,0,NULL),(6,'50-60',6,'2026-10-23 00:00:00.000000',14,150.00,0.00,0.00,150.00,NULL,'2026-10-23 11:46:01.390830','Cash',0,NULL,0,1,NULL)
 ON DUPLICATE KEY UPDATE
  `OfficialReceipt` = VALUES(`OfficialReceipt`),
  `PayeeID` = VALUES(`PayeeID`),
  `DateIssued` = VALUES(`DateIssued`),
  `CollectedBy_UserID` = VALUES(`CollectedBy_UserID`),
  `TotalBaseAmount` = VALUES(`TotalBaseAmount`),
  `TotalSurcharge` = VALUES(`TotalSurcharge`),
  `TotalInterest` = VALUES(`TotalInterest`),
  `TotalAmount` = VALUES(`TotalAmount`),
  `Remarks` = VALUES(`Remarks`),
  `CreatedAt` = VALUES(`CreatedAt`),
  `PaymentMethod` = VALUES(`PaymentMethod`),
  `HasPendingRequest` = VALUES(`HasPendingRequest`),
  `CollectedConfirmedAt` = VALUES(`CollectedConfirmedAt`),
  `IsCollected` = VALUES(`IsCollected`),
  `IsDeleted` = VALUES(`IsDeleted`),
  `PayorFullName` = VALUES(`PayorFullName`);
ALTER TABLE `PaymentRecords` AUTO_INCREMENT = 7;

-- ---------------------------------------------------------------------
-- Table: `RecordLineItems`
-- ---------------------------------------------------------------------
INSERT INTO `RecordLineItems` (`LineItemID`, `PaymentID`, `TypeID`, `Quantity`, `BaseAmount`, `SurchargeAmount`, `InterestAmount`, `LineTotal`) VALUES
(1,1,15,1,800.00,0.00,0.00,800.00),(2,2,6,1,300.00,0.00,0.00,300.00),(3,3,18,46,8000.00,0.00,0.00,8000.00),(4,4,4,1,599.80,0.00,0.00,599.80),(5,5,3,1,1200.00,0.34,0.00,1200.34),(6,6,5,11,150.00,0.00,0.00,150.00)
 ON DUPLICATE KEY UPDATE
  `PaymentID` = VALUES(`PaymentID`),
  `TypeID` = VALUES(`TypeID`),
  `Quantity` = VALUES(`Quantity`),
  `BaseAmount` = VALUES(`BaseAmount`),
  `SurchargeAmount` = VALUES(`SurchargeAmount`),
  `InterestAmount` = VALUES(`InterestAmount`),
  `LineTotal` = VALUES(`LineTotal`);
ALTER TABLE `RecordLineItems` AUTO_INCREMENT = 7;

-- ---------------------------------------------------------------------
-- Table: `Editrequests`
-- ---------------------------------------------------------------------
INSERT INTO `Editrequests` (`RequestID`, `PaymentID`, `RequestedBy_UserID`, `Reason`, `Status`, `ReviewedBy_UserID`, `ReviewNote`, `CreatedAt`, `ReviewedAt`, `OfficialReceipt`) VALUES
(1,4,14,'wrong amount','Resolved',1,NULL,'2026-11-05 15:13:58.826145','2026-11-05 15:16:21.544513','254522'),(2,5,14,'change amount','Resolved',1,NULL,'2026-11-05 15:17:23.148023','2026-11-05 15:22:40.658626','119119'),(3,3,14,'test','Pending',NULL,NULL,'2026-11-05 15:43:07.944313',NULL,'255-300')
 ON DUPLICATE KEY UPDATE
  `PaymentID` = VALUES(`PaymentID`),
  `RequestedBy_UserID` = VALUES(`RequestedBy_UserID`),
  `Reason` = VALUES(`Reason`),
  `Status` = VALUES(`Status`),
  `ReviewedBy_UserID` = VALUES(`ReviewedBy_UserID`),
  `ReviewNote` = VALUES(`ReviewNote`),
  `CreatedAt` = VALUES(`CreatedAt`),
  `ReviewedAt` = VALUES(`ReviewedAt`),
  `OfficialReceipt` = VALUES(`OfficialReceipt`);
ALTER TABLE `Editrequests` AUTO_INCREMENT = 4;

-- ---------------------------------------------------------------------
-- Table: `DeletedRecords`
-- ---------------------------------------------------------------------
INSERT INTO `DeletedRecords` (`DeletedRecordID`, `PaymentID`, `PaymentRecordPaymentID`, `PayeeName`, `CollectorName`, `CollectionType`, `DeletedBy_UserID`, `DeletedByUserID`, `DeletedAt`, `DeletedByName`, `OfficialReceipt`) VALUES
(18,6,NULL,', Water','Lim, Leksha','Water Fees',1,NULL,'2026-11-05 14:58:30.539855','Tolomea, Gabriel','50-60')
 ON DUPLICATE KEY UPDATE
  `PaymentID` = VALUES(`PaymentID`),
  `PaymentRecordPaymentID` = VALUES(`PaymentRecordPaymentID`),
  `PayeeName` = VALUES(`PayeeName`),
  `CollectorName` = VALUES(`CollectorName`),
  `CollectionType` = VALUES(`CollectionType`),
  `DeletedBy_UserID` = VALUES(`DeletedBy_UserID`),
  `DeletedByUserID` = VALUES(`DeletedByUserID`),
  `DeletedAt` = VALUES(`DeletedAt`),
  `DeletedByName` = VALUES(`DeletedByName`),
  `OfficialReceipt` = VALUES(`OfficialReceipt`);
ALTER TABLE `DeletedRecords` AUTO_INCREMENT = 19;

-- ---------------------------------------------------------------------
-- Table: `ReportLog`
-- ---------------------------------------------------------------------
INSERT INTO `ReportLog` (`LogID`, `ReportType`, `Format`, `GeneratedAt`, `GeneratedByUserID`, `ReportKind`, `FromDate`, `ToDate`, `RevenueTypeID`, `GeneratedByName`) VALUES
(1,'Monthly Revenue Summary','PDF','2026-10-29 22:49:37.707341',1,'monthly','2026-10-01 00:00:00.000000','2026-10-29 00:00:00.000000',NULL,'Gabriel Tolomea'),(2,'Monthly Revenue Summary','PDF','2026-11-04 10:09:47.055638',1,'monthly','2026-11-01 00:00:00.000000','2026-11-04 00:00:00.000000',NULL,'Gabriel Tolomea')
 ON DUPLICATE KEY UPDATE
  `ReportType` = VALUES(`ReportType`),
  `Format` = VALUES(`Format`),
  `GeneratedAt` = VALUES(`GeneratedAt`),
  `GeneratedByUserID` = VALUES(`GeneratedByUserID`),
  `ReportKind` = VALUES(`ReportKind`),
  `FromDate` = VALUES(`FromDate`),
  `ToDate` = VALUES(`ToDate`),
  `RevenueTypeID` = VALUES(`RevenueTypeID`),
  `GeneratedByName` = VALUES(`GeneratedByName`);
ALTER TABLE `ReportLog` AUTO_INCREMENT = 3;

-- ---------------------------------------------------------------------
-- Refresh Table Statistics and Auto-Increment Cache
-- ---------------------------------------------------------------------
ANALYZE TABLE
  `__EFMigrationsHistory`,
  `RevenueCategories`,
  `RevenueTypes`,
  `RevenuePolicies`,
  `BillingTypeOptions`,
  `UserAccounts`,
  `LongTermPayees`,
  `AccountBillingTypes`,
  `MonthlyBills`,
  `Payees`,
  `PaymentRecords`,
  `RecordLineItems`,
  `Editrequests`,
  `DeletedRecords`,
  `ReportLog`;

-- =====================================================================
-- Restore Session Configurations
-- =====================================================================
/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;
