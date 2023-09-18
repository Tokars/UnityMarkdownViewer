#if UNITY_2018_1_OR_NEWER

using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
#if UNITY_2020_2_OR_NEWER
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace MD.Editor
{
    [ScriptedImporter( 1, "markdown" )]
    public class MarkdownAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset( AssetImportContext ctx )
        {
            var md = new TextAsset( File.ReadAllText( ctx.assetPath ) );
            ctx.AddObjectToAsset( "main", md );
            ctx.SetMainObject( md );
        }
    }
}

#endif
