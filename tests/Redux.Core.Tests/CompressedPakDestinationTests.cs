using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using LSLib.LS;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Redux.Core.Tests;

public sealed class CompressedPakDestinationTests
{
	public void UnsafeMetadataDestinationNamesAreRejected()
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxCompressedDestinationTests", "Mods");
		foreach (var folder in new[] { "../escaped", "..\\escaped", "C:\\escaped", "\\escaped", "folder/file", "file:stream", "CON", "LPT1", "", ".." })
		{
			var mod = new DivinityModData { Name = "Example Mod", Folder = folder, HasMetadata = true };
			RegressionAssert.Throws<InvalidDataException>(() => CompressedPakDestination.Resolve(root, "download.pak.xz", mod));
		}
	}

	public void ValidMetadataRenamesRemainCompatibleAndOverridesKeepSourceFilename()
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxCompressedDestinationTests", "Mods");
		var metadata = new DivinityModData { Name = "Example Mod", Folder = "ExampleFolder", HasMetadata = true };
		RegressionAssert.Equal(Path.Combine(root, "ExampleFolder.pak"), CompressedPakDestination.Resolve(root, "download.xz", metadata));
		RegressionAssert.Equal(Path.Combine(root, "Example Mod.pak"), CompressedPakDestination.Resolve(root, "Example Mod.pak.zst", metadata));
		var fileOverride = new DivinityModData { Name = ".random-import-staging.pak", Folder = "Game", HasMetadata = false, IsForceLoaded = true };
		RegressionAssert.Equal(Path.Combine(root, "MyOverride.pak"), CompressedPakDestination.Resolve(root, "MyOverride.pak.bz2", fileOverride));
	}

	public void RealPakWithParentRelativeMetadataCannotChooseOutsideDestination()
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxCompressedDestinationTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var source = Path.Combine(root, "download.pak");
			const string xml = """
				<save><version major="4" minor="8" revision="0" build="0"/><region id="Config"><node id="root"><children><node id="ModuleInfo"><attribute id="UUID" type="FixedString" value="e7d72274-9348-4075-bbd7-392bc37824cf"/><attribute id="Name" type="LSString" value="Containment Fixture"/><attribute id="Author" type="LSString" value="Audit"/><attribute id="Folder" type="LSString" value="../escaped"/><attribute id="Type" type="FixedString" value="Add-on"/><attribute id="Version64" type="int64" value="36028797018963968"/></node></children></node></region></save>
				""";
			var build = new PackageBuildData();
			build.Files.Add(new PackageBuildInputFile { Path = "Mods/Example/meta.lsx", Body = Encoding.UTF8.GetBytes(xml) });
			using (var writer = PackageWriterFactory.Create(build, source)) writer.Write();
			var original = File.ReadAllBytes(source);
			var mod = DivinityModDataLoader.LoadModDataFromPakAsync(source, new Dictionary<string, DivinityModData>(), CancellationToken.None)
				.GetAwaiter().GetResult();
			RegressionAssert.True(mod != null);
			RegressionAssert.Equal("../escaped", mod.Folder);
			RegressionAssert.Throws<InvalidDataException>(() => CompressedPakDestination.Resolve(Path.Combine(root, "Mods"), "download.pak.xz", mod));
			RegressionAssert.SequenceEqual(original, File.ReadAllBytes(source));
			RegressionAssert.False(File.Exists(Path.Combine(root, "escaped.pak")));
		}
		finally { Directory.Delete(root, true); }
	}
}
