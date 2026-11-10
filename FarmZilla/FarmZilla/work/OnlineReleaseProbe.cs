using System;using System.Net;using System.Text;
class OnlineReleaseProbe {
 static void Main(string[] args){string service=args[0];string url=AppUpdate.ManifestUrl(service);AppUpdate.ClientAuthorization=null;
  bool denied=false;try{AppUpdate.Download(url,AppUpdate.MaxManifest);}catch(WebException e){var response=e.Response as HttpWebResponse;denied=response!=null&&(int)response.StatusCode==401;if(response!=null)response.Dispose();}if(!denied)throw new Exception("Manifesto não bloqueou acesso anônimo.");Console.WriteLine("PASS manifesto não pode ser baixado sem autorização");
  var cache=OnlineLicense.Load();if(cache==null||String.IsNullOrEmpty(cache.Token))throw new Exception("Ative o PC do administrador para verificar a release.");AppUpdate.ClientAuthorization=cache.Token;
  string envelope=Encoding.UTF8.GetString(AppUpdate.Download(url,AppUpdate.MaxManifest));var manifest=AppUpdate.Verify(envelope,service,ReleaseInfo.PublicKey);if(manifest.version!=ReleaseInfo.Version)throw new Exception("Versão remota "+manifest.version+" diferente da versão esperada "+ReleaseInfo.Version+". Aguarde a propagação e confira novamente.");byte[] bytes=AppUpdate.Download(manifest.url,AppUpdate.MaxExe);AppUpdate.VerifyBinary(bytes,manifest);Console.WriteLine("PASS release online "+manifest.version+" tem assinatura, tamanho e SHA-256 corretos");
  AppUpdate.ClientAuthorization=null;denied=false;try{AppUpdate.Download(manifest.url,AppUpdate.MaxExe);}catch(WebException e){var response=e.Response as HttpWebResponse;denied=response!=null&&(int)response.StatusCode==401;if(response!=null)response.Dispose();}if(!denied)throw new Exception("Executável permitiu acesso anônimo.");Console.WriteLine("PASS link direto do executável também exige autorização");
 }
}

