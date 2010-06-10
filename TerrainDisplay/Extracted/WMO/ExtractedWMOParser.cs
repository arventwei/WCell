﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MPQNav.MPQ.WMO.Components;
using MPQNav.Util;

namespace TerrainDisplay.Extracted.WMO
{
    public static class ExtractedWMOParser
    {
        private const string fileType = "wmo";

        public static ExtractedWMO Process(string basePath, int mapId, string path)
        {
            basePath = Path.Combine(basePath, mapId.ToString());
            var filePath = Path.Combine(basePath, path);
            filePath = Path.ChangeExtension(filePath, ".wmo");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Extracted M2 file not found: {0}", filePath);
            }

            var wmo = new ExtractedWMO();

            using (var file = File.OpenRead(filePath))
            using (var br = new BinaryReader(file))
            {
                var type = br.ReadString();
                if (type != fileType)
                {
                    br.Close();
                    throw new InvalidDataException(string.Format("WMO file in invalid format: {0}", filePath));
                }

                wmo.Extents = br.ReadBoundingBox();
                wmo.WMOId = br.ReadUInt32();

                ReadWMODoodadDefs(br, wmo);

                ReadWMOGroups(br, wmo);
            }
            return wmo;
        }

        private static void ReadWMODoodadDefs(BinaryReader br, ExtractedWMO wmo)
        {
            var numSets = br.ReadInt32();
            var setList = new List<Dictionary<int, ExtractedWMOM2Definition>>(numSets);
            for (var i = 0; i < numSets; i++)
            {
                var numDefs = br.ReadInt32();
                var defDict = new Dictionary<int, ExtractedWMOM2Definition>(numDefs);
                for (var j = 0; j < numDefs; j++)
                {
                    var key = br.ReadInt32();
                    var def = new ExtractedWMOM2Definition {
                                                               FilePath = br.ReadString(),
                                                               Position = br.ReadVector3(),
                                                               Extents = br.ReadBoundingBox(),
                                                               WMOToModel = br.ReadMatrix(),
                                                               ModeltoWMO = br.ReadMatrix()
                                                           };
                    defDict.Add(key, def);
                }
                setList.Add(defDict);
            }
            wmo.WMOM2Defs = setList;
        }

        private static void ReadWMOGroups(BinaryReader br, ExtractedWMO wmo)
        {
            var groupCount = br.ReadInt32();
            var groupList = new List<ExtractedWMOGroup>(groupCount);
            for (int i = 0; i < groupCount; i++)
            {
                var group = new ExtractedWMOGroup();
                group.Flags = (WMOGroupFlags) br.ReadUInt32();
                group.Bounds = br.ReadBoundingBox();
                group.GroupId = br.ReadUInt32();
                group.ModelRefs = br.ReadInt32List();
                group.Vertices = br.ReadVector3List();

                ReadBSPTree(br, group);

                groupList.Add(group);
            }
            wmo.Groups = groupList;
        }

        private static void ReadBSPTree(BinaryReader br, ExtractedWMOGroup group)
        {
            var rootId = br.ReadInt16();
            var nodeCount = br.ReadInt32();
            var nodeList = new List<BSPNode>(nodeCount);
            for (var i = 0; i < nodeCount; i++)
            {
                var node = new BSPNode {
                                           flags = (BSPNodeFlags) br.ReadByte(),
                                           negChild = br.ReadInt16(),
                                           posChild = br.ReadInt16(),
                                           planeDist = br.ReadSingle(),
                                           TriIndices = br.ReadIndex3List()
                                       };

                nodeList.Add(node);
            }

            group.Tree = new BSPTree(nodeList, rootId);
        }
    }
}
