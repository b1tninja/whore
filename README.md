whore
=====

**whore** -- 'work' over tor -- Is an onion service which accepts a target hostname, performs a series of analysis tasks on the target, then caches and returns the result to a client. Given extensive usage, this means *whore* will build up a large database of queryable information about hosts.

The client-agnostic implementation will allow for a variety of interfaces, from a simple command-line query tool to a full graphical interface or web-based frontend. 

Installation
------------

### Under Mono (Best for \*nix platforms)

Prerequisites:

+ GNU `make`
+ The Mono C# Compiler `mcs` [tested with v 3.0.70]
+ The Tor executable [Available from <https://www.torproject.org/download/download-easy.html.en>]
+ A MySql DB
+ The Mysql Connector/Net Library


The Tor executable doesn't need to be in your $PATH, just download and extract the zipfile somewhere. The first thing you need to do is create the whore database. The default connection string presumes a user *justin* with no password needed to access the *whore* database, you can alter these settings as you like.

    mysql> CREATE USER justin;
    mysql> CREATE DATABASE whore;

The file `database.sql` creates the table structure necessary for **whore** to operate. [NB: May not be exact as of current commit]

    $ mysql -u root -p whore < bootstrap.sql

Don't forget to grant local access to the program user.

    mysql> GRANT ALL PRIVILEGES ON whore.* TO justin@localhost; 

Presuming everything's gone well, you should be able to compile the system by typing

    $ make

If you get errors, you may want to check that $PKG\_CONFIG\_PATH includes the folder where `mysql.data.pc` is installed. You may also have to alter the Makefile to point it at your `System.Data.dll` file. 

If compilation succeeds, then you can attempt to launch the program. Typing

    $ make run

Will, without modifying the Makefile, run the executable with the database configuration string provided there and with the result of `locate App/tor` as the location of your Tor executable. Modify these arguments as required. 


Development Tasks
-----------------------

###Complete/Ongoing
+ Threads to launch multiple tor processes
+ TorTask - basic structure is there, delegate handler code could use some work
+ DnsTask - does single queries, and capable of caching to db. Needs to be able to recurse to the delegated nameservers, till it gets an authoratative (or answer from all nameservers?). 
+ WhoisTask - uses cname lookup of tld.whois-servers.net, handles in-addr arpa for ipv4 and 6. Should resolve the cname with a DnsTask, not direct lookup. Does not cache to db currently.

###Unimplemented/Wishlist
+ PortTask - Perform a port scan on the host. 
+ HttpTask 
+ HttpsTask
+ FtpTask
+ Simple Client (+Server Interface)
+ ???
	
	
Discussion:
------------------------
1. The dataset could one day become too large to store in a centralized fashion (Too expensive, anyway). Decentralising the database as a DHT or similar could prevent this, but we would need a mechanism to protect against the insertion of junk data.
 
2. It might be worth looking at `torsocks` as a library for the tor tasks, rather than manipulating the tor executable ourselves.	
