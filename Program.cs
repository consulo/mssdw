using System;
using System.Threading;
using Consulo.Internal.Mssdw;
using Consulo.Internal.Mssdw.Request;

public class Program {
    public static void Main(String[] args) {
        DebugSession session = new DebugSession();
        session.CodeFileLoaded += delegate(DebugSession arg1, string fileName) {
            Console.WriteLine("code file loaded: " + fileName);

            BreakpointRequestResult result = session.InsertBreakpoint(new BreakpointRequest(fileName, 6));

            Console.WriteLine(result);
        };

        session.Start(args);

        while (!session.Finished) {
            Thread.Sleep(500);
        }
    }
}
