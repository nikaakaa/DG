using ExtensibilityVerification;

if (!DummyRunnerExtensibilityVerification.Run(out string reason))
{
    Console.Error.WriteLine(reason);
    return 1;
}

Console.WriteLine("Dummy runner extensibility verification passed.");
return 0;
