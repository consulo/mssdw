//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------


// These interfaces serve as an extension to the BCL's SymbolStore interfaces.
namespace Microsoft.Samples.Debugging.CorSymbolStore 
{
    using System.Diagnostics.SymbolStore;
    
    using System;
    using System.Text;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.ComTypes;

    [
        ComVisible(false)
    ]
    public interface ISymbolBinder2
    {
        ISymbolReader GetReaderForFile(object importer, string filename, string searchPath);
                                
        ISymbolReader GetReaderForFile(object importer, string fileName,
                                           string searchPath, SymSearchPolicies searchPolicy);
        
        ISymbolReader GetReaderForFile(object importer, string fileName,
                                           string searchPath, SymSearchPolicies searchPolicy,
                                           IntPtr callback);
      
        ISymbolReader GetReaderFromStream(object importer, IStream stream);
    }
}
