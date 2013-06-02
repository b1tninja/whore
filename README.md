whore
=====

whore -- 'work' over tor -- Is an onion service which accepts a target hostname, performs a series of analysis tasks on the target, then caches and returns the result to a client. Given extensive usage, this means whore will build up a large database of queryable information about hosts.

The client-agnostic implementation will allow for a variety of interfaces, from a simple command-line query tool to a full graphical interface or web-based frontend. 


In progress/implemented:
-----------------------
+ Threads to launch multiple tor processes
+ TorTask - basic structure is there, delegate handler code could use some work
+ DnsTask - does single queries, and capable of caching to db. Needs to be able to recurse to the delegated nameservers, till it gets an authoratative (or answer from all nameservers?). 
+ WhoisTask - uses cname lookup of tld.whois-servers.net, handles in-addr arpa for ipv4 and 6. Should resovle the cname with a DnsTask, not direct lookup. Does not cache to db currently.
+ ???

Feature reqs/todo:
-----------------
+ PortTask
+ HttpTask
+ HttpsTask
+ FtpTask
+ Simple Client (+Server Interface)
+ ???
	
	
Discussion/complications:
------------------------
The dataset could one day be too large to store in a centralized fashion. (Too expensive anyway) And if decentralized, how to prevent junk data
	
	
