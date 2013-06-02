# 
# Makefile for Whore 
#
# Compiler: Mono C# compiler version 3.0.7.0
# Author: balan
#

# Point this at your 

LSTSFT=Starksoft.Net.Proxy.dll
TORLOCATION=`locate App/tor`
DBCONFIG='server=127.0.0.1;userid=justin;database=whore'
SOURCE=whore/db.cs  whore/dns.cs  whore/task.cs  whore/tor.cs  whore/whois.cs  whore/whore.cs
CC=mcs
PKG=-pkg:mysql.data
#May have to generalise the System.Data requirement.
RES=-r:$(LSTSFT) -r:/usr/lib/mono/4.0/System.Data.dll
OUT=whore.exe

whore.exe : $(LSTSFT) $(SOURCE) 
	$(CC) -debug $(PKG) $(RES) $(SOURCE) -out:$(OUT) 

clean : $(OUT)
	rm $(OUT)

run : $(OUT) 
	mono --debug $(OUT)  $(DBCONFIG) $(TORLOCATION) 