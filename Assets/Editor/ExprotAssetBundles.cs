
	using UnityEngine;
	using System.Collections;
	using UnityEditor;
	using System.Collections.Generic;
	using System.Xml;
	using System.IO;
	using System.Text;
	using LitJson;
	using System.Linq;
	using System.Text.RegularExpressions;
	
    public class ExportAssetBundles {  
    	public static int uuid = 0;
    	public static int TERRAIN_SPLIT_SIZE = 128;
		public static bool currentScene = false;
		public static HashSet<string> refPrefabs = new HashSet<string>();
		
    	static string[] parseCmdAndName(string fullname)
    	{
    		int startIdx = -1;
    		int endIdx = -1;
    		
    		int lstartIdx = -1;
    		int lendIdx = -1;
    		
    		string[] s = new string[3];
    		s[0] = "";
    		s[1] = fullname;
    		s[2] = "99";
    		
    		for(int i=0; i<fullname.Length; i++)
    		{
    			if(startIdx == -1 && fullname[i] == '[')
    			{
    				startIdx = i;
    			}
    			
    			if(endIdx == -1 && fullname[i] == ']')
    			{
    				endIdx = i;
    			}
    			
    			if(lstartIdx == -1 && fullname[i] == '(')
    			{
    				lstartIdx = i;
    			}
    			
    			if(lendIdx == -1 && fullname[i] == ')')
    			{
    				lendIdx = i;
    			}
    		}
    		
    		if(startIdx != -1 && endIdx != -1)
    		{
	    		string cmd = fullname.Substring(startIdx + 1, endIdx - startIdx - 1);
	    		string name = fullname.Substring(endIdx + 1, fullname.Length - endIdx - 1);
	    		// Debug.Log("parseCmdAndName: fullname=" + fullname + ", cmd=" + cmd + ", name=" + name);
	    		
	    		s[0] = cmd;
	    		s[1] = name;
    		}

    		if(lstartIdx != -1 && lendIdx != -1 && lendIdx > lstartIdx && lstartIdx == 0)
    		{
	    		string level = fullname.Substring(lstartIdx + 1, lendIdx - lstartIdx - 1);
	    		s[2] = level;
    		}
    		
    		return s;
    	}
    	
        //[MenuItem("Publish/Build AssetBundle From Selection - Track dependencies")]  
        static void ExportResource () {  
            // Bring up save panel  
            string path = EditorUtility.SaveFilePanel ("Save Resource", "", "New Resource", "unity3d");  
            if (path.Length != 0) {  
                // Build the resource file from the active selection.  
                Object[] selection = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);  
                BuildPipeline.BuildAssetBundle(Selection.activeObject, selection, path, BuildAssetBundleOptions.CollectDependencies | BuildAssetBundleOptions.CompleteAssets);  
                Selection.objects = selection;  
            }  
        }  
       // [MenuItem("Publish/Build AssetBundle From Selection - No dependency tracking")]  
        static void ExportResourceNoTrack () {  
            // Bring up save panel  
            string path = EditorUtility.SaveFilePanel ("Save Resource", "", "New Resource", "unity3d");  
            if (path.Length != 0) {  
                // Build the resource file from the active selection.  
                BuildPipeline.BuildAssetBundle(Selection.activeObject, Selection.objects, path);  
            }  
        } 
        
	    static void getScenes(DirectoryInfo curr, List<FileInfo> lst)
	    {
			foreach(DirectoryInfo NextFolder in curr.GetDirectories())
			{
				if(NextFolder.FullName.IndexOf("StreamingAssets") > -1 || NextFolder.FullName.IndexOf("thirdparty") > -1)
					continue;
				
				foreach(FileInfo NextFile in NextFolder.GetFiles())
				{
					// string filename = System.IO.Path.GetFileName(fullPath);//文件名  “Default.aspx”
					string extension = System.IO.Path.GetExtension(NextFile.Name);//扩展名 “.aspx”
					string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(NextFile.Name);// 没有扩展名的文件名 “Default”
					
					if(currentScene == true && fileNameWithoutExtension == System.IO.Path.GetFileNameWithoutExtension(EditorApplication.currentScene) ||
					currentScene == false)
					{
						if(extension == ".unity")
						{
							// Debug.Log (NextFile.FullName); 
							lst.Add(NextFile);
						}
					}
				}
				
				getScenes(NextFolder, lst);
			}
	    }
	
	    static void getUnity3ds(DirectoryInfo curr, List<FileInfo> lst)
	    {
			foreach(DirectoryInfo NextFolder in curr.GetDirectories())
			{
				if(NextFolder.FullName.IndexOf("StreamingAssets") != -1)
					continue;
			
				foreach(FileInfo NextFile in NextFolder.GetFiles())
				{
					// string filename = System.IO.Path.GetFileName(fullPath);//文件名  “Default.aspx”
					string extension = System.IO.Path.GetExtension(NextFile.Name);//扩展名 “.aspx”
					if(extension == ".unity3d")
					{
						lst.Add(NextFile);
					}
				}
				
				getUnity3ds(NextFolder, lst);
			}
	    }
	
		static void writeLoadLevel(XmlElement gameObject, string [] names)
		{
			gameObject.SetAttribute("loadPri", names[2]);
			
			string cmd = names[0];
			string loadlevel = "0";
			string unloadlevel = "0";
			
			if(cmd.IndexOf("*") != -1)
			{
				loadlevel = "2";
			}
			
			if(cmd.IndexOf("!") != -1)
			{
				loadlevel = "1";
			}
	
			if(cmd.IndexOf("-") != -1)
			{
				loadlevel = "3";
			}

			if(cmd.IndexOf("#") != -1)
			{
				loadlevel = "4";
			}
			
			if(cmd.IndexOf("+") != -1)
			{
				unloadlevel = "1";
			}

			if(cmd.IndexOf("&") != -1)
			{
				unloadlevel = "2";
			}
			
			gameObject.SetAttribute("load", loadlevel);
			gameObject.SetAttribute("unload", unloadlevel);
		}
		
		static void createWorldPrefabsAndWriteXML(XmlDocument xmlDoc, XmlElement root, GameObject obj, string scenename)
		{
			if(root == null)
			{
				XmlElement WorldNode = xmlDoc.CreateElement("gameObject");
				WorldNode.SetAttribute("name", "world");
				root.AppendChild(WorldNode);
				root = WorldNode;
			}
			
			foreach(Transform child in obj.transform)
			{
				if(PrefabUtility.GetPrefabParent(child.gameObject) != null)
				{	
					addGameObjectToParent(xmlDoc, root, child.gameObject);
					continue;
				}
				
				if(child.gameObject.name == "Terrain" || child.gameObject.name == "terrain")
				{
					continue;
				}
				
				string path = "Assets/StreamingAssets/_prefabs/streaming_" + scenename + "_" + child.gameObject.name + ("." + getUUID()) + ".prefab";
				path = path.Replace(" ", "_");
				Object prefab = PrefabUtility.CreateEmptyPrefab(path);
				PrefabUtility.ReplacePrefab(child.gameObject, prefab, ReplacePrefabOptions.ConnectToPrefab);
				refPrefabs.Add(path);
				addGameObjectToParent(xmlDoc, root, child.gameObject);
			}
		}
		
		static string getUUID()
		{
			if(currentScene == true)
				return System.Guid.NewGuid().ToString();
			uuid += 1;
			return uuid + "";
		}
		
		static void addGameObjectToParent(XmlDocument xmlDoc, XmlElement rootxml, GameObject obj)
		{
			if(obj.name == "kbengine" || obj.name == "world" || obj.name == "Terrain" || obj.name == "terrain")
				return;
			
			string [] names = parseCmdAndName(obj.name);
			
			XmlElement gameObject = xmlDoc.CreateElement("gameObject");
			gameObject.SetAttribute("name", names[1]);
			gameObject.SetAttribute("id", getUUID());
			gameObject.SetAttribute("layer", LayerMask.LayerToName(obj.layer));
			writeLoadLevel(gameObject, names);
			
			if(PrefabUtility.GetPrefabParent(obj) == null)
			{
				gameObject.SetAttribute("asset", "");
			}
			else
			{
				gameObject.SetAttribute("asset", PrefabUtility.GetPrefabParent(obj).name + ".unity3d");
				refPrefabs.Add(AssetDatabase.GetAssetPath(PrefabUtility.GetPrefabParent(obj)));
			}
			
			XmlElement transform = xmlDoc.CreateElement("transform");
			XmlElement position = xmlDoc.CreateElement("position");
			XmlElement position_x = xmlDoc.CreateElement("x");
			position_x.InnerText = obj.transform.position.x+"";
			XmlElement position_y = xmlDoc.CreateElement("y");
			position_y.InnerText = obj.transform.position.y+"";
			XmlElement position_z = xmlDoc.CreateElement("z");
			position_z.InnerText = obj.transform.position.z+"";
			position.AppendChild(position_x);
			position.AppendChild(position_y);
			position.AppendChild(position_z);
	
			XmlElement rotation = xmlDoc.CreateElement("rotation");
			XmlElement rotation_x = xmlDoc.CreateElement("x");
			rotation_x.InnerText = obj.transform.rotation.eulerAngles.x+"";
			XmlElement rotation_y = xmlDoc.CreateElement("y");
			rotation_y.InnerText = obj.transform.rotation.eulerAngles.y+"";
			XmlElement rotation_z = xmlDoc.CreateElement("z");
			rotation_z.InnerText = obj.transform.rotation.eulerAngles.z+"";
			rotation.AppendChild(rotation_x);
			rotation.AppendChild(rotation_y);
			rotation.AppendChild(rotation_z);
	
			XmlElement scale = xmlDoc.CreateElement("scale");
			XmlElement scale_x = xmlDoc.CreateElement("x");
			scale_x.InnerText = obj.transform.localScale.x+"";
			XmlElement scale_y = xmlDoc.CreateElement("y");
			scale_y.InnerText = obj.transform.localScale.y+"";
			XmlElement scale_z = xmlDoc.CreateElement("z");
			scale_z.InnerText = obj.transform.localScale.z+"";
	
			scale.AppendChild(scale_x);
			scale.AppendChild(scale_y);
			scale.AppendChild(scale_z);
	
			transform.AppendChild(position);
			transform.AppendChild(rotation);
			transform.AppendChild(scale);	
	
			gameObject.AppendChild(transform);
			rootxml.AppendChild(gameObject);
						
			if(PrefabUtility.GetPrefabParent(obj) != null)
				return;
			
			foreach(Transform child in obj.transform){
				addGameObjectToParent(xmlDoc, gameObject, child.gameObject);
			}
		}
		
		static void addSpawnPointToParent(XmlDocument xmlDoc, XmlElement rootxml, GameObject obj)
		{
			foreach(Transform child in obj.transform)
			{
				if(PrefabUtility.GetPrefabParent(child.gameObject) != null)
				{
					XmlElement gameObject = xmlDoc.CreateElement("gameObject");
					gameObject.SetAttribute("name", child.gameObject.name);
					refPrefabs.Add(AssetDatabase.GetAssetPath(PrefabUtility.GetPrefabParent(child.gameObject)));
					XmlElement transform = xmlDoc.CreateElement("transform");
					XmlElement position = xmlDoc.CreateElement("position");
					XmlElement position_x = xmlDoc.CreateElement("x");
					position_x.InnerText = child.gameObject.transform.position.x + "";
					XmlElement position_y = xmlDoc.CreateElement("y");
					position_y.InnerText = child.gameObject.transform.position.y + "";
					XmlElement position_z = xmlDoc.CreateElement("z");
					position_z.InnerText = child.gameObject.transform.position.z + "";
					position.AppendChild(position_x);
					position.AppendChild(position_y);
					position.AppendChild(position_z);
					
					XmlElement direction = xmlDoc.CreateElement("direction");
					XmlElement direction_x = xmlDoc.CreateElement("x");
					direction_x.InnerText = child.gameObject.transform.rotation.eulerAngles.z + "";
					XmlElement direction_y = xmlDoc.CreateElement("y");
					direction_y.InnerText = child.gameObject.transform.rotation.eulerAngles.x + "";
					XmlElement direction_z = xmlDoc.CreateElement("z");
					direction_z.InnerText = child.gameObject.transform.rotation.eulerAngles.y + "";
					direction.AppendChild(direction_x);
					direction.AppendChild(direction_y);
					direction.AppendChild(direction_z);

					XmlElement scale = xmlDoc.CreateElement("scale");
					XmlElement scale_x = xmlDoc.CreateElement("x");
					scale_x.InnerText = child.gameObject.transform.localScale.x + "";
					XmlElement scale_y = xmlDoc.CreateElement("y");
					scale_y.InnerText = child.gameObject.transform.localScale.y + "";
					XmlElement scale_z = xmlDoc.CreateElement("z");
					scale_z.InnerText = child.gameObject.transform.localScale.z + "";
					scale.AppendChild(scale_x);
					scale.AppendChild(scale_y);
					scale.AppendChild(scale_z);
					
					transform.AppendChild(position);
					transform.AppendChild(direction);
					transform.AppendChild(scale);
					gameObject.AppendChild(transform);
					rootxml.AppendChild(gameObject);
				}
			}
		}

		//将所有游戏场景导出为XML格式
		//[MenuItem ("Publish/Build AssetXMLInfo")]
		static void ExportXML()
		{
			if(!File.Exists("Assets/StreamingAssets"))
				Directory.CreateDirectory("Assets/StreamingAssets");
			
			if(!File.Exists("Assets/StreamingAssets/_prefabs"))
				Directory.CreateDirectory("Assets/StreamingAssets/_prefabs");
			
			List<FileInfo> lst = new List<FileInfo>();
			
			getScenes(new DirectoryInfo(Application.dataPath), lst);
			
			string lastScene = "";
			
			uuid = 0;
			
			foreach(FileInfo NextFile in lst)
			{
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(NextFile.FullName);
	            if(fileNameWithoutExtension == "go" || fileNameWithoutExtension == "gameui")
	            	continue;
	
				if(NextFile.FullName.IndexOf("thirdparty") > -1)
					continue;
				
			    string filepath = Application.dataPath + @"/StreamingAssets/" + fileNameWithoutExtension + ".xml";
				if(!File.Exists (filepath))
				{
					File.Delete(filepath);
				}
				
			    string spawnpoints_filepath = Application.dataPath + @"/StreamingAssets/" + fileNameWithoutExtension + "_spawnpoints.xml";
				if(!File.Exists (spawnpoints_filepath))
				{
					File.Delete(spawnpoints_filepath);
				}
				
				XmlDocument spawnpoints_xmlDoc = new XmlDocument();
				XmlElement spawnpoints_root = spawnpoints_xmlDoc.CreateElement("root");
				
				XmlDocument xmlDoc = new XmlDocument();
				XmlElement root = xmlDoc.CreateElement("root");
				
				string[] res = Regex.Split(NextFile.FullName, "Assets");
				string name = "Assets" + res[1];
				
				// 打开这个关卡
				if(currentScene == false)
					EditorApplication.OpenScene(name);
				
				root.SetAttribute("name", name);
				
				XmlElement renderSettingNode = xmlDoc.CreateElement("gameObject");
				renderSettingNode.SetAttribute("name", "renderSettings");
				
				if(RenderSettings.fog == true)
					renderSettingNode.SetAttribute("fog", "true");
				else
					renderSettingNode.SetAttribute("fog", "false");
				
				if(RenderSettings.fogMode == FogMode.Linear)
					renderSettingNode.SetAttribute("fogMode", 1 + "");
				else if(RenderSettings.fogMode == FogMode.Exponential)
					renderSettingNode.SetAttribute("fogMode", 2 + "");
				else if(RenderSettings.fogMode == FogMode.ExponentialSquared)
					renderSettingNode.SetAttribute("fogMode", 3 + "");
				
				renderSettingNode.SetAttribute("fogDensity", RenderSettings.fogDensity.ToString());
				renderSettingNode.SetAttribute("fogStartDistance", RenderSettings.fogStartDistance.ToString());
				renderSettingNode.SetAttribute("fogEndDistance", RenderSettings.fogEndDistance.ToString());
				renderSettingNode.SetAttribute("fogColor_r", RenderSettings.fogColor.r.ToString());
				renderSettingNode.SetAttribute("fogColor_g", RenderSettings.fogColor.g.ToString());
				renderSettingNode.SetAttribute("fogColor_b", RenderSettings.fogColor.b.ToString());
				renderSettingNode.SetAttribute("fogColor_a", RenderSettings.fogColor.a.ToString());
				
				renderSettingNode.SetAttribute("ambientLight_r", RenderSettings.ambientLight.r.ToString());
				renderSettingNode.SetAttribute("ambientLight_g", RenderSettings.ambientLight.g.ToString());
				renderSettingNode.SetAttribute("ambientLight_b", RenderSettings.ambientLight.b.ToString());
				renderSettingNode.SetAttribute("ambientLight_a", RenderSettings.ambientLight.a.ToString());
				
				renderSettingNode.SetAttribute("haloStrength", RenderSettings.haloStrength.ToString());
				renderSettingNode.SetAttribute("flareStrength", RenderSettings.flareStrength.ToString());
				
				renderSettingNode.SetAttribute("skybox", RenderSettings.skybox != null ? RenderSettings.skybox.name.Replace(" ", "_") + ".unity3d": "");
				
				root.AppendChild(renderSettingNode);
				
				lastScene = name;
				XmlElement worldroot = streamWorld(xmlDoc, root);
				
				foreach (GameObject obj in Object.FindObjectsOfType(typeof(GameObject)))
				{
					if (obj.transform.parent == null)
					{
						if(obj.name.IndexOf("#") != -1)
						{
							addSpawnPointToParent(spawnpoints_xmlDoc, spawnpoints_root, obj);
							spawnpoints_xmlDoc.AppendChild(spawnpoints_root);
							continue;
						}
						
						if(PrefabUtility.GetPrefabParent(obj) == null && 
							obj.name != "kbengine" && 
							obj.name != "Terrain" && 
							obj.name != "terrain")
						{
							if(obj.name == "world")
							{
								createWorldPrefabsAndWriteXML(xmlDoc, worldroot, obj, fileNameWithoutExtension);
								continue;
							}
							string[] names = parseCmdAndName(obj.name);
							
							string path = "Assets/StreamingAssets/_prefabs/streaming_" + fileNameWithoutExtension + "_" + names[1] + ("." + getUUID()) + ".prefab";
							path = path.Replace(" ", "_");
							Object prefab = PrefabUtility.CreateEmptyPrefab(path);
							PrefabUtility.ReplacePrefab(obj.gameObject, prefab, ReplacePrefabOptions.ConnectToPrefab);
							refPrefabs.Add(path);
						}

						addGameObjectToParent(xmlDoc, root, obj);
						xmlDoc.AppendChild(root);
					}
				}
			
				xmlDoc.Save(filepath);
				spawnpoints_xmlDoc.Save(spawnpoints_filepath);
			}
				
			// 刷新Project视图， 不然需要手动刷新哦
			AssetDatabase.Refresh();
			if(lastScene != "" && currentScene == false)
				EditorApplication.OpenScene(lastScene);
		}
		
	    static void getPrefabs(DirectoryInfo curr, List<FileInfo> lst)
	    {
			foreach(string path in refPrefabs)
			{
				FileInfo NextFile = new FileInfo(path);
				lst.Add(NextFile);
			}
			
			return;
		
			foreach(DirectoryInfo NextFolder in curr.GetDirectories())
			{
				if(NextFolder.FullName.IndexOf("thirdparty") > -1)
					continue;
				
				foreach(FileInfo NextFile in NextFolder.GetFiles())
				{
					// string filename = System.IO.Path.GetFileName(fullPath);//文件名  “Default.aspx”
					string extension = System.IO.Path.GetExtension(NextFile.Name);//扩展名 “.aspx”
					// string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(NextFile.Name);// 没有扩展名的文件名 “Default”
	
					if(extension == ".prefab")
					{
						// Debug.Log (NextFile.FullName); 
						lst.Add(NextFile);
					}
				}
				
				getPrefabs(NextFolder, lst);
			}
	    }
		
	    // [MenuItem("Publish/Build Publish AssetBundles - autoCurrent")]  
	    static void ExportResourceAutoCurrent() 
		{
			if(!File.Exists("Assets/StreamingAssets"))
				Directory.CreateDirectory("Assets/StreamingAssets");
			
			currentScene = true;
			ExportXML();
		}
		
	    //[MenuItem("Publish/export shaders")]  
	    static void ExportCoreShaderAutoAll() 
		{
			string path = "Assets/StreamingAssets/shader_Terrain_Diffuse.unity3d";
			Debug.Log(Shader.Find("Nature/Terrain/Diffuse"));
			BuildPipeline.BuildAssetBundle(Shader.Find("Nature/Terrain/Diffuse"), null, path);  
		}
		
	    [MenuItem("Publish/Build Publish AssetBundles(打包所有需要动态加载资源)")]  
	    static void ExportResourceAutoAll() 
		{
			AssetDatabase.DeleteAsset("Assets/StreamingAssets");
			Directory.CreateDirectory("Assets/StreamingAssets"); 
			
			currentScene = false;
			ExportXML();
			
			List<FileInfo> lst = new List<FileInfo>();
			getPrefabs(new DirectoryInfo(Application.dataPath), lst);
			
			foreach(FileInfo NextFile in lst)
			{
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(NextFile.Name);// 没有扩展名的文件名 “Default”
	            string path = "Assets/StreamingAssets/" + fileNameWithoutExtension + ".unity3d";
				
				string[] res = Regex.Split(NextFile.FullName, "Assets");
				string path1 = "";
				for(int i=1; i<res.Length; i++)
				{
					path1 += "Assets";
					path1 += res[i];
				}
			
				Object t = AssetDatabase.LoadMainAssetAtPath(path1);
				Debug.Log("path=" + path1);
				Debug.Log("Object=" + t);
			
				try
				{
					// Build the resource file from the active selection.  
					BuildPipeline.BuildAssetBundle(t, null, path);  
					// BuildPipeline.BuildAssetBundle(t, null, path, BuildAssetBundleOptions.CollectDependencies | BuildAssetBundleOptions.CompleteAssets);
				}catch(System.Exception ex)
				{
					Debug.LogError(ex.Message.ToString());
				}
			}
			
			lst.Clear();
			getScenes(new DirectoryInfo(Application.dataPath), lst);
			List<string> skyboxres = new List<string>();
			
			foreach(FileInfo NextFile in lst)
			{
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(NextFile.FullName);
	            if(fileNameWithoutExtension == "go" || fileNameWithoutExtension == "gameui")
	            	continue;
				
				string[] res = Regex.Split(NextFile.FullName, "Assets");
				string name = "Assets" + res[1];
				
	           // 打开这个关卡
				EditorApplication.OpenScene(name);
				
				if(RenderSettings.skybox != null)
				{
					bool created = false;
					foreach(string mname in skyboxres)
					{
						if(RenderSettings.skybox.name == mname)
						{
							created = true;
							break;
						}
					}
					
					if(created == false)
					{
						skyboxres.Add(RenderSettings.skybox.name);
			            string path = "Assets/StreamingAssets/" + RenderSettings.skybox.name.Replace(" ", "_") + ".unity3d";
						BuildPipeline.BuildAssetBundle(RenderSettings.skybox, null, path);  
					}
				}
			}
		
			lst.Clear();
			getUnity3ds(new DirectoryInfo(Application.dataPath), lst);
			foreach(FileInfo NextFile in lst)
			{
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(NextFile.FullName);
				System.IO.File.Copy(NextFile.FullName, "Assets/StreamingAssets/" + fileNameWithoutExtension + ".unity3d");
			}
			
            Debug.Log("Everything built successfully! 生成成功！");
            EditorUtility.DisplayDialog("Everything built successfully! 生成成功！", "Everything built successfully! 生成成功！", " OK 确定");
	    } 
	    
		static XmlElement streamWorld(XmlDocument xmlDoc, XmlElement root)
		{
			XmlElement WorldNode = xmlDoc.CreateElement("gameObject");
			WorldNode.SetAttribute("name", "world");
			root.AppendChild(WorldNode);
			WorldNode.SetAttribute("wname", System.IO.Path.GetFileNameWithoutExtension(EditorApplication.currentScene));
			
			root = WorldNode;
			GameObject obj = GameObject.Find("Terrain");
			if(obj == null)
				return null;
			
			XmlElement TerrainNode = xmlDoc.CreateElement("gameObject");
			TerrainNode.SetAttribute("name", "Terrain");
	
			root.AppendChild(TerrainNode);
			root = TerrainNode;
			
			TerrainData terrainData = obj.GetComponent<TerrainCollider>().terrainData;
			
			Debug.Log("StreamWorld:" + terrainData.name);
			
			HashSet<string> refPrefabs = new HashSet<string>();
			
			XmlElement tmpNode = xmlDoc.CreateElement("size");
			XmlElement prefabsNode = xmlDoc.CreateElement("prefabs");
			root.AppendChild(prefabsNode);
			
			tmpNode.InnerText = (terrainData.size.x + " " + terrainData.size.y + " " + terrainData.size.z);
			root.AppendChild(tmpNode);
	
			tmpNode = xmlDoc.CreateElement("name");
			tmpNode.InnerText = terrainData.name;
			root.AppendChild(tmpNode);
			
			int splitsize = 0;
		    foreach (Transform child in obj.transform)
		    {
		    	string name = child.gameObject.name;
		    	if(name == "2")
		    	{
		    		splitsize = 2;
		    	}
		    	else if(name == "4")
		    	{
		    		splitsize = 4;
		    	}
				else if(name == "8")
		    	{
		    		splitsize = 8;
		    	}
		    	else if(name == "16")
		    	{
		    		splitsize = 16;
		    	}
		    	else if(name == "64")
		    	{
		    		splitsize = 64;
		    	}
			};
			
			if(splitsize == 0)
			{
				Debug.LogError("splitsize: is 0");
				splitsize = 8;
			}
		
			tmpNode = xmlDoc.CreateElement("splitSize");
			tmpNode.InnerText = (splitsize + "");
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("alphamapWH");
			tmpNode.SetAttribute("w", terrainData.alphamapWidth + "");
			tmpNode.SetAttribute("h", terrainData.alphamapHeight + "");
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("baseMapResolution");
			tmpNode.InnerText = (terrainData.baseMapResolution + "");
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("detailResolution");
			tmpNode.InnerText = (terrainData.detailResolution + "");
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("detailWH");
			tmpNode.SetAttribute("w", terrainData.detailWidth + "");
			tmpNode.SetAttribute("h", terrainData.detailHeight + "");
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("heightmapWH");
			tmpNode.SetAttribute("w", terrainData.heightmapWidth + "");
			tmpNode.SetAttribute("h", terrainData.heightmapHeight + "");
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("heightmapResolution");
			tmpNode.InnerText = (terrainData.heightmapResolution + "");
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("heightmapScale");
			tmpNode.InnerText = (terrainData.heightmapScale.x + " " + terrainData.heightmapScale.y + " " + terrainData.heightmapScale.z); 
			root.AppendChild(tmpNode);
	
			tmpNode = xmlDoc.CreateElement("hideFlags");
			tmpNode.InnerText = (terrainData.hideFlags + "");
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("wavingGrassAmount");
			tmpNode.InnerText = (terrainData.wavingGrassAmount + "");
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("wavingGrassSpeed");
			tmpNode.InnerText = (terrainData.wavingGrassSpeed + "");
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("wavingGrassStrength");
			tmpNode.InnerText = (terrainData.wavingGrassStrength + "");
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("wavingGrassTint");
			tmpNode.InnerText = (terrainData.wavingGrassTint.r + " " + terrainData.wavingGrassTint.g + " " + terrainData.wavingGrassTint.b + " " + terrainData.wavingGrassTint.a); 
			root.AppendChild(tmpNode);
			
			tmpNode = xmlDoc.CreateElement("treePrototypes");
			for(int i=0; i<terrainData.treePrototypes.Length; i++)
			{
				TreePrototype tp = terrainData.treePrototypes[i];
				XmlElement itemNode = xmlDoc.CreateElement("item");
				
				itemNode.SetAttribute("prefab", tp.prefab.name);
				refPrefabs.Add(tp.prefab.name);
				
				XmlElement prefabsItemNode = xmlDoc.CreateElement("item");
				prefabsItemNode.InnerText = tp.prefab.name;
				//prefabsNode.AppendChild(prefabsItemNode);
				
				itemNode.SetAttribute("bendFactor", tp.bendFactor + "");
				
				tmpNode.AppendChild(itemNode);
				
			    string path = "Assets/StreamingAssets/" + tp.prefab.name.Replace(" ", "_") + ".unity3d";
				BuildPipeline.BuildAssetBundle(tp.prefab, null, path);  
			}
			root.AppendChild(tmpNode);
			
			/*
			tmpNode = xmlDoc.CreateElement("treeInstances");
			for(int i=0; i<terrainData.treeInstances.Length; i++)
			{
				TreeInstance ti = terrainData.treeInstances[i];
				XmlElement itemNode = xmlDoc.CreateElement("item");
				ti.
				itemNode.SetAttribute("prefab", tp.prefab.name);
				itemNode.SetAttribute("bendFactor", tp.bendFactor + "");
				
				tmpNode.AppendChild(itemNode);
			}
			root.AppendChild(tmpNode);
			*/
			
			tmpNode = xmlDoc.CreateElement("detailPrototypes");
			for(int i=0; i<terrainData.detailPrototypes.Length; i++)
			{
				DetailPrototype dp = terrainData.detailPrototypes[i];
				XmlElement itemNode = xmlDoc.CreateElement("item");
				
				itemNode.SetAttribute("prefab", dp.prototypeTexture.name);
				refPrefabs.Add(dp.prototypeTexture.name);
				itemNode.SetAttribute("bendFactor", dp.bendFactor + "");
				itemNode.SetAttribute("usePrototypeMesh", dp.usePrototypeMesh + "");
				
				XmlElement prefabsItemNode = xmlDoc.CreateElement("item");
				prefabsItemNode.InnerText = dp.prototypeTexture.name;
				//prefabsNode.AppendChild(prefabsItemNode);
				
				tmpNode.AppendChild(itemNode);
				
			    string path = "Assets/StreamingAssets/" + dp.prototypeTexture.name.Replace(" ", "_") + ".unity3d";
				BuildPipeline.BuildAssetBundle(dp.prototypeTexture, null, path);  
			}
			root.AppendChild(tmpNode);
			
			XmlElement splatprotosNode = xmlDoc.CreateElement("splatprotos");
			
			for(int i=0; i<terrainData.splatPrototypes.Length; i++)
			{
				SplatPrototype sp = terrainData.splatPrototypes[i];
				XmlElement itemNode = xmlDoc.CreateElement("item");
				
				string texture = sp.texture != null ? sp.texture.name : "";
				if(texture != "")
				{
					XmlElement prefabsItemNode = xmlDoc.CreateElement("item");
					prefabsItemNode.InnerText = texture;
					//prefabsNode.AppendChild(prefabsItemNode);
					refPrefabs.Add(texture);
					
				    string path = "Assets/StreamingAssets/" + texture.Replace(" ", "_") + ".unity3d";
					BuildPipeline.BuildAssetBundle(sp.texture, null, path);  
				}
				
				itemNode.SetAttribute("texture", texture);
				
				texture = sp.normalMap != null ? sp.normalMap.name : "";
				if(texture != "")
				{
					XmlElement prefabsItemNode = xmlDoc.CreateElement("item");
					prefabsItemNode.InnerText = texture;
					//prefabsNode.AppendChild(prefabsItemNode);
					refPrefabs.Add(texture);
					
				    string path = "Assets/StreamingAssets/" + texture.Replace(" ", "_") + ".unity3d";
					BuildPipeline.BuildAssetBundle(sp.normalMap, null, path);  
				}
					
				itemNode.SetAttribute("normalMap", sp.normalMap != null ? sp.normalMap.name : "");
				itemNode.SetAttribute("tileOffsetX", sp.tileOffset.x + "");
				itemNode.SetAttribute("tileOffsetY", sp.tileOffset.y + "");
				itemNode.SetAttribute("tileSizeX", sp.tileSize.x + "");
				itemNode.SetAttribute("tileSizeY", sp.tileSize.y + "");
				
				splatprotosNode.AppendChild(itemNode);
			}
			
			root.AppendChild(splatprotosNode);
			return WorldNode;
		}
}