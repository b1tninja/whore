whore
=====
whore does "work" over tor, caches the results, and offers a interface to access the data through a distributed hash table system, be it a webfrontend, or possibly a more advanced gui application that perhaps could interface the webfrontend via json API. The "work" consists of things like port scans, web crawlers, dns/whois lookups. 

In progress/implemented:
	Threads to launch multiple tor processes
	TorTask - basic structure is there, delegate handler code could use some work
	DnsTask - does single queries, and capable of caching to db. Needs to be able to recurse to the delegated nameservers, till it gets an authoratative (or answer from all nameservers?). 
	WhoisTask - uses cname lookup of tld.whois-servers.net, handles in-addr arpa for ipv4 and 6. Should resovle the cname with a DnsTask, not direct lookup. Does not cache to db currently.
	???

Feature reqs/todo:
	PortTask
	HttpTask
	HttpsTask
	FtpTask
	???
	
	
Discussion/complications:
	The dataset could one day be too large to store in a centralized fashion. (Too expensive anyway) And if decentralized, how to prevent junk data
	
	