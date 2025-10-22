using SDL3_Sandbox.UO;

namespace SDL3_Sandbox;

class Program
{
    static void Main(string[] args)
    {
        var app = new App();

        app.Init(ClientVersion.CV_706400, "/home/kaczy/nel/Ultima Online Classic_7_0_95_0_modified");
        app.Run();
    }
}