--
-- Table structure for table `labels`
--

DROP TABLE IF EXISTS `labels`;


CREATE TABLE `labels` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `parent` int(10) unsigned DEFAULT NULL,
  `label` varchar(64) NOT NULL,
  `cached` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `parent` (`parent`),
  KEY `label` (`label`)
) ENGINE=InnoDB AUTO_INCREMENT=0 DEFAULT CHARSET=utf8;

--
-- Table structure for table `queries`
--

DROP TABLE IF EXISTS `queries`;


CREATE TABLE `queries` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `label` int(10) unsigned NOT NULL,
  `type` smallint(5) unsigned NOT NULL,
  `class` smallint(5) unsigned NOT NULL,
  PRIMARY KEY (`id`),
  KEY `label_idx` (`label`),
  CONSTRAINT `label` FOREIGN KEY (`label`) REFERENCES `labels` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB AUTO_INCREMENT=0 DEFAULT CHARSET=utf8;


--
-- Table structure for table `records`
--

DROP TABLE IF EXISTS `records`;

CREATE TABLE `records` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `query` int(10) unsigned NOT NULL,
  `ttl` int(10) unsigned NOT NULL,
  `data` blob,
  `cached` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `source` int(10) unsigned DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `query_idx` (`query`),
  CONSTRAINT `query` FOREIGN KEY (`query`) REFERENCES `queries` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB AUTO_INCREMENT=0 DEFAULT CHARSET=utf8;
