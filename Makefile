# 
# Makefile for Whore 
#
# Compiler: Mono C# compiler version 3.0.7.0
# Author: balan
#
# This Makefile is intended for Linux users compiling
# and running Whore under the Mono .Net framework. 

LSTSFT=Starksoft.Net.Proxy.dll
TORLOCATION=`locate App/tor`
DBCONFIG='server=127.0.0.1;userid=justin;database=whore'
SOURCE=src/db.cs  src/dns.cs  src/task.cs  src/tor.cs  src/whois.cs  src/whore.cs src/clientinterface.cs src/Properties/AssemblyInfo.cs
CLSOURCE=src/clients/clclient/clclient.cs
CLEXE=clclient.exe
CLOUT=client
CC=mcs
PKG=-pkg:mysql.data
RES=-r:$(LSTSFT) -r:System.Data.dll
OUT=whore.exe

$(OUT) : $(LSTSFT) $(SOURCE) 
	$(CC) -debug $(PKG) $(RES) $(SOURCE) -out:$(OUT) 

$(CLEXE) : $(CLSOURCE)
	$(CC) $(CLSOURCE) -out:$(CLEXE) 

$(CLOUT) : $(CLEXE)
	echo "#!/bin/bash" > $(CLOUT)
	echo "mono $(CLEXE) \$$@" >> $(CLOUT)
	chmod +x $(CLOUT)

clean : 
	rm $(OUT) $(OUT).mdb $(CLOUT) $(CLEXE)

run : $(OUT) 
	mono --debug $(OUT)  $(DBCONFIG) $(TORLOCATION) $(LSTSFT)
