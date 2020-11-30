using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

using UnityEditor;
using UnityEditor.Experimental.AssetImporters;

namespace Carambolas.UnityEditor
{
    [ScriptedImporter(1, "cfg")]
    class ConfigFileImporter: ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var asset = new TextAsset(File.ReadAllText(ctx.assetPath));
            ctx.AddObjectToAsset("Text", asset);
            ctx.SetMainObject(asset);
        }
    }
}
