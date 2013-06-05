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

The file `database.sql` creates the table structure necessary for **whore** to operate. 

    $ mysql -u root -p whore < database.sql

Don't forget to grant local access to the program user.

    mysql> GRANT ALL PRIVILEGES ON whore.* TO justin@localhost; 

Presuming everything's gone well, you should be able to compile the system by typing

    $ make

If you get errors, you may want to check that $PKG\_CONFIG\_PATH includes the folder where `mysql.data.pc` is installed. You may also have to alter the Makefile to point it at your `System.Data.dll` file. 

If compilation succeeds, then you can attempt to launch the program. Typing

    $ make run

Will, without modifying the Makefile, run the server executable with the database configuration string provided there and with the result of `locate App/tor` as the location of your Tor executable. Modify these arguments as required. 

To run a client to connect to a **whore** server, build it by typing

    $ make client

Then run it with

    $ ./client <host> <port> <commandstring>

Where *host* and *port* are the host and port of the **whore** server (typically localhost and 9051), and a *commandstring* is a series of comma-separated task keywords followed by a colon and then the host you wish to target. For example:

    $ ./client localhost 9051 dns:www.example.com

Will run a DNS lookup on `www.example.com`. 


Development Tasks
-----------------------

###Complete/Ongoing
+ Threads to launch multiple tor processes
+ TorTask - basic structure is there, delegate handler code could use some work
+ DnsTask - does single queries, and capable of caching to db. Needs to be able to recurse to the delegated nameservers, till it gets an authoratative (or answer from all nameservers?). Currently an issue to run.  
+ WhoisTask - uses cname lookup of tld.whois-servers.net, handles in-addr arpa for ipv4 and 6. Should resolve the cname with a DnsTask, not direct lookup. Does not cache to db currently.
+ Simple Client (+Server Interface), only on TCP connection at the moment, should probably move to a Tor connection. 

###Unimplemented/Wishlist
+ PortTask - Perform a port scan on the host. 
+ HttpTask 
+ HttpsTask
+ FtpTask
+ ???
	
	
Discussion:
------------------------
1. The dataset could one day become too large to store in a centralized fashion (Too expensive, anyway). Decentralising the database as a DHT or similar could prevent this, but we would need a mechanism to protect against the insertion of junk data.
 
2. It might be worth looking at `torsocks` as a library for the tor tasks, rather than manipulating the tor executable ourselves.	

3. Suggestion for dealing with the data storage and return-to-client issues: Compile the results of all tasks requested on a host into a nicely-formatted text/markdown report which can then be posted to Pastebin (or a similar service). We can then respond to the client's request with a link to the full report on the tasks they requested. This lightens our load considerably (we only have to keep URLs in long-term storage, and only partially-created reports in short-term caches before submitting to the paste service) and lets people share their results easily.
