namespace DgDevelopment.Identity.Server.Security;

using QRCoder;

internal static class TotpQrCodeRenderer
{
    public static string BuildDataUri(Uri provisioningUri)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(provisioningUri.ToString(), QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        return $"data:image/png;base64,{Convert.ToBase64String(qrCode.GetGraphic(20))}";
    }
}
