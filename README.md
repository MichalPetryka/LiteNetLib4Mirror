# LiteNetLib4Mirror
LiteNetLib based transport for Mirror.  

# Features
- UDP  
- Built-in Network Discovery and UPnP  
- Fully managed code  
Also, since it's using LiteNetLib, it supports it's following features:  
- Small CPU and RAM usage  
- Small packet size overhead ( 1 byte for unreliable, 3 bytes for reliable packets )  
- Different send mechanics  
- Reliable with order  
- Reliable without order  
- Ordered but unreliable with duplication prevention  
- Simple UDP packets without order and reliability  
- Automatic small packets merging  
- Automatic fragmentation of reliable packets  
- Automatic MTU detection  
- NTP time requests  
- Packet loss and latency simulation  
- IPv6 support (dual mode)  
- Connection statisitcs (need DEBUG or STATS_ENABLED flag)  
- Multicasting (for discovering hosts in local network)  

# Warning!
With IL2CPP, IPv6 is only supported on Unity 2018.3.6f1 and later because of this:  
![alt text](https://prnt.sc/mp3dxn)

# Credits
RevenantX - for LiteNetLib  
vis2k & Paul - for Mirror  
Coburn - for Ignorance which i've used as an example  
Dankrushen - for helping me find one small mistake which i couldn't find for two days  
Lucas Ontivero - for Open.Nat, used for UPnP  
