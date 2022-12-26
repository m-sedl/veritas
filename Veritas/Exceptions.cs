namespace Veritas;

public class MethodNotFoundException : Exception
{
    public MethodNotFoundException(int token): base($"Method with token {token} not found") {}
}