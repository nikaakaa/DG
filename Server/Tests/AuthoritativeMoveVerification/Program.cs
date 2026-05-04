using Fantasy;

if (!AuthoritativeMoveWorldVerification.Run(out string reason))
{
    Console.Error.WriteLine(reason);
    return 1;
}

Console.WriteLine("Authoritative move verification passed.");
return 0;
