Uses .NET Standard 2.0 assemblies from the Azure Artifacts
skyline-private-nugets feed:

- SLNetTypes.dll and slnettypes.xml:
  Skyline.DataMiner.Internal.SLNetTypes 10.6.2643-434
- SLUnitConverter.dll:
  Skyline.DataMiner.Internal.SLUnitConverter 10.6.2643-434
- SLProtoBufLibrary.dll:
  Skyline.DataMiner.Internal.SLProtoBufLibrary 10.6.2642-1288
- Newtonsoft.Json.dll: Newtonsoft.Json 13.0.4 (lib/netstandard2.0)

Extract the DataMiner DLLs from lib/netstandard2.0 in these packages.
Keep the direct assembly references so public builds do not require access
to the private feed. SLNetTypes embeds SLBinaryFormatter and its other
internal runtime dependencies; do not extract those embedded assemblies.
The old SLNetTypes.deps.json described the 10.4 build and is not needed
by these direct references.

SLNetTypes uses DataMiner's embedded SLBinaryFormatter instead of the
system BinaryFormatter removed in .NET 9 and later. It does not require
EnableUnsafeBinaryFormatterSerialization.

ICSharpCode.SharpZipLib.dll (1.3.3.11) and protobuf-net.dll (2.4.8.0)
are unchanged.

Need to use the .NETStandard assemblies. The DevPacks use .NETFramework.
When using devpacks you'll get exceptions with:

System.Runtime.Serialization.SerializationException: Type 'System.Security.Cryptography.RSAParameters' 
in Assembly 'System.Security.Cryptography.Algorithms, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' is not marked as serializable.


This is caused by differences in the Serializable attributes between .NET Core and .NET Framework for that class.