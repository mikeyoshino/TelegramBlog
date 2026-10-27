using Foundation;
using Security;
using TeleGramBlog.Telegram;
namespace Luma.Services;

public sealed class KeychainSessionStore(string service = "app.telegramblog.luma.session", string account = "telegram") : ISessionStore
{
    readonly object gate = new();
    SecRecord Query() => new(SecKind.GenericPassword)
    { Service = service, Account = account };
    public byte[]? Load()
    {
        lock (gate)
        {
            using var query = Query();
            using var data = SecKeyChain.QueryAsData(query, false, out var status);
            if (status == SecStatusCode.ItemNotFound) return null;
            if (status != SecStatusCode.Success) throw new IOException("Cannot read the protected Telegram session.");
            return data?.ToArray();
        }
    }
    public void Save(byte[] bytes)
    {
        lock (gate)
        {
            using var query = Query();
            // Update attributes must not contain kSecClass; it belongs only in the query.
            using var value = new SecRecord()
            { ValueData = NSData.FromArray(bytes), Accessible = SecAccessible.AfterFirstUnlockThisDeviceOnly };
            var status = SecKeyChain.Update(query, value);
            if (status == SecStatusCode.ItemNotFound)
            {
                query.ValueData = value.ValueData;
                query.Accessible = value.Accessible;
                status = SecKeyChain.Add(query);
            }
            if (status != SecStatusCode.Success) throw new IOException($"Cannot protect the Telegram session in Keychain ({status}).");
        }
    }
    public void Clear()
    {
        lock (gate)
        {
            using var query = Query();
            var status = SecKeyChain.Remove(query);
            if (status is not (SecStatusCode.Success or SecStatusCode.ItemNotFound))
                throw new IOException("Cannot remove the Telegram session from Keychain.");
        }
    }
}
