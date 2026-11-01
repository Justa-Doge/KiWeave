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
            string signature, publicKey;
            try {
                // StreamReader's previous ASCII-based reader still detected and removed a UTF-8 BOM.
                // Keep that compatibility while retaining strict UTF-8 validation and the size bound.
                signature = AtomicFile.ReadAllTextBounded(signaturePath, 4096, new UTF8Encoding(true, true)).Trim();
                publicKey = AtomicFile.ReadAllTextBounded(publicKeyPath, 8192, new UTF8Encoding(true, true));
            } catch (InvalidDataException ex) { throw new ArgumentException("Action-pack signature sidecar is too large.", ex); }
            try { using (var rsa = new RSACryptoServiceProvider()) { rsa.FromXmlString(publicKey); bool valid = rsa.VerifyData(Encoding.UTF8.GetBytes(json), CryptoConfig.MapNameToOID("SHA256"), Convert.FromBase64String(signature)); if (!valid) throw new ArgumentException("Action-pack signature verification failed."); } return "Detached RSA signature verified"; } catch (FormatException) { throw new ArgumentException("Action-pack signature is not valid base64."); } catch (CryptographicException ex) { throw new ArgumentException("Action-pack public key is invalid: " + ex.Message); }
        }
    }
}
