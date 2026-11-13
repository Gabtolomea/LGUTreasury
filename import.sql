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
-- Refresh Table Statistics and Auto-Increment Cache
-- ---------------------------------------------------------------------
ANALYZE TABLE
  `RevenueCategories`,
  `RevenueTypes`;

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
