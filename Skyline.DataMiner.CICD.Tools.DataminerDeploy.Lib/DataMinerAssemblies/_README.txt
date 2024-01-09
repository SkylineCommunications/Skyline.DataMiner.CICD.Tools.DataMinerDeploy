Uses assemblies from 10.4.1.0

Need to use the .NETStandard assemblies. The DevPacks use .NETFramework.
When using devpacks you'll get exceptions with:

System.Runtime.Serialization.SerializationException: Type 'System.Security.Cryptography.RSAParameters' 
in Assembly 'System.Security.Cryptography.Algorithms, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' is not marked as serializable.


This seems to be because they changes the Serializeable attributes between .NETCore and .NETFramework for that class.