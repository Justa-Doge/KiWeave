using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class ActionPackSignatures
    {
        internal static string Status(string packPath, string json)
        {
            string signaturePath = packPath + ".sig", publicKeyPath = packPath + ".pub"; if (!File.Exists(signaturePath) && !File.Exists(publicKeyPath)) return "Unsigned detached pack"; if (!File.Exists(signaturePath) || !File.Exists(publicKeyPath)) throw new ArgumentException("Signed action packs require both .sig and .pub sidecars.");
            string signature = File.ReadAllText(signaturePath, Encoding.ASCII).Trim(), publicKey = File.ReadAllText(publicKeyPath, Encoding.UTF8); if (signature.Length > 4096 || publicKey.Length > 8192) throw new ArgumentException("Action-pack signature sidecar is too large.");
            try { using (var rsa = new RSACryptoServiceProvider()) { rsa.FromXmlString(publicKey); bool valid = rsa.VerifyData(Encoding.UTF8.GetBytes(json), CryptoConfig.MapNameToOID("SHA256"), Convert.FromBase64String(signature)); if (!valid) throw new ArgumentException("Action-pack signature verification failed."); } return "Detached RSA signature verified"; } catch (FormatException) { throw new ArgumentException("Action-pack signature is not valid base64."); } catch (CryptographicException ex) { throw new ArgumentException("Action-pack public key is invalid: " + ex.Message); }
        }
    }
}
