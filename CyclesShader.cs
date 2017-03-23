﻿/**
Copyright 2014-2017 Robert McNeel and Associates

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
**/
using System;
using ccl;
using Rhino.DocObjects;
using Rhino.Render;
using RhinoCyclesCore.Materials;
using RhinoCyclesCore.Converters;

namespace RhinoCyclesCore
{

	/// <summary>
	/// Intermediate class to convert various Rhino shader types
	/// to Cycles shaders
	///
	/// @todo better organise shader intermediary code instead of overloading heavily
	/// </summary>
	public class CyclesShader
	{
		private ShaderBody _front;
		private ShaderBody _back;
		public CyclesShader(uint id)
		{
			Id = id;
			_front = null;
			_back = null;

		}

		/// <summary>
		/// RenderHash of the RenderMaterial for which this intermediary is created.
		/// </summary>
		public uint Id { get; }

		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			var other = obj as CyclesShader;

			return other != null && Id.Equals(other.Id);
		}

		public bool CreateFrontShader(RenderMaterial rm, float gamma)
		{
			_front = new ShaderBody(Id);
			return CreateShaderPart(_front, rm, gamma);
		}

		public void FrontXmlShader(string name, ICyclesMaterial crm)
		{
			_front = new ShaderBody(Id)
			{
				Name = name,
				Crm = crm,
				CyclesMaterialType = ShaderBody.CyclesMaterial.Xml
			};
		}

		public bool CreateBackShader(RenderMaterial rm, float gamma)
		{
			_back = new ShaderBody(Id);
			return CreateShaderPart(_back, rm, gamma);
		}

		public ShaderBody Front => _front;
		public ShaderBody Back => _back;

		public bool DisplayMaterial => _front != null && _back != null;

		public bool ValidDisplayMaterial =>
			_front?.CyclesMaterialType != ShaderBody.CyclesMaterial.Xml
			&&
			_back?.CyclesMaterialType != ShaderBody.CyclesMaterial.Xml;

		public float Gamma { get; set; }

		private enum ProbableMaterial
		{
			Plaster,
			Picture,
			Paint,
			Glass,
			Gem,
			Plastic,
			Metal,
			Custom
		}
		private static ProbableMaterial WhatMaterial(RenderMaterial rm, Rhino.DocObjects.Material m)
		{
			if (rm.TypeId.Equals(RenderMaterial.PictureMaterialGuid))
			{
				return ProbableMaterial.Picture;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.PlasterMaterialGuid))
			{
				return ProbableMaterial.Plaster;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.GlassMaterialGuid))
			{
				return ProbableMaterial.Glass;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.GemMaterialGuid))
			{
				return ProbableMaterial.Gem;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.PaintMaterialGuid))
			{
				return ProbableMaterial.Paint;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.PlasticMaterialGuid))
			{
				return ProbableMaterial.Plastic;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.MetalMaterialGuid))
			{
				return ProbableMaterial.Metal;
			}


			if (rm.SmellsLikePlaster || rm.SmellsLikeTexturedPlaster)
			{
				return ProbableMaterial.Plaster;
				
			}
			if (rm.SmellsLikeGlass || rm.SmellsLikeTexturedGlass)
			{
				return ProbableMaterial.Glass;
				
			}
			if (rm.SmellsLikeGem || rm.SmellsLikeTexturedGem)
			{
				return ProbableMaterial.Gem;
				
			}
			if (rm.SmellsLikePaint || rm.SmellsLikeTexturedPaint)
			{
				return ProbableMaterial.Paint;
				
			}
			if (rm.SmellsLikePlastic || rm.SmellsLikeTexturedPlastic)
			{
				return ProbableMaterial.Plastic;
				
			}
			if (rm.SmellsLikeMetal || rm.SmellsLikeTexturedMetal)
			{
				return ProbableMaterial.Metal;
			}

			return ProbableMaterial.Custom;
		}

		private bool CreateShaderPart(ShaderBody shb, RenderMaterial rm, float gamma)
		{
			var crm = rm as ICyclesMaterial;
			ShaderBody.CyclesMaterial mattype = ShaderBody.CyclesMaterial.No;
			if (crm == null)
			{
				// always simulate material, need to know now myself
				// what to read out from the simulated material to
				// populate my own material descriptions.
				var m = rm.SimulateMaterial(true);
				// figure out what type of material we are.
				//var probemat = GuessMaterialFromSmell(rm);
				var probemat = WhatMaterial(rm, m);

				rm.BeginChange(RenderContent.ChangeContexts.Ignore);
				var dcl = m.DiffuseColor;
				var scl = m.SpecularColor;
				var rcl = m.ReflectionColor;
				var rfcl = m.TransparentColor;
				var emcl = m.EmissionColor;
				var polish = (float)m.ReflectionGlossiness;
				var reflectivity = (float)m.Reflectivity;
				var metalic = 0f;
				var shine = (float)(m.Shine / Material.MaxShine);

				switch (probemat)
				{
					case ProbableMaterial.Plaster:
						mattype = ShaderBody.CyclesMaterial.Diffuse;
						break;
					case ProbableMaterial.Glass:
					case ProbableMaterial.Gem:
						metalic = 0f;
						mattype = ShaderBody.CyclesMaterial.Glass;
						break;
					case ProbableMaterial.Metal:
						metalic = 1.0f;
						mattype = ShaderBody.CyclesMaterial.SimpleMetal;
						break;
					case ProbableMaterial.Plastic:
						//polish = reflectivity;
						//shine = polish;
						//reflectivity = 0f;
						metalic = 0f;
						mattype = ShaderBody.CyclesMaterial.SimplePlastic;
						break;
					case ProbableMaterial.Paint:
						mattype = ShaderBody.CyclesMaterial.Paint;
						break;
					case ProbableMaterial.Custom:
						mattype = ShaderBody.CyclesMaterial.No;
						break;
				}

				var difftexAlpha = m.AlphaTransparency;

				var col = RenderEngine.CreateFloat4(dcl.R, dcl.G, dcl.B, 255);
				var spec = RenderEngine.CreateFloat4(scl.R, scl.G, scl.B, 255);
				var refl = RenderEngine.CreateFloat4(rcl.R, rcl.G, rcl.B, 255);
				var transp = RenderEngine.CreateFloat4(rfcl.R, rfcl.G, rfcl.B, 255);
				var refr = RenderEngine.CreateFloat4(rfcl.R, rfcl.G, rfcl.B, 255);
				var emis = RenderEngine.CreateFloat4(emcl.R, emcl.G, emcl.B, 255);

				//shb.Type = CyclesShader.Shader.Diffuse,
				shb.CyclesMaterialType = mattype;

				shb.Shadeless = m.DisableLighting;

				shb.DiffuseColor = col;
				shb.SpecularColor = spec;
				shb.ReflectionColor = refl;
				shb.ReflectionRoughness = (float)m.ReflectionGlossiness; // polish;
				shb.RefractionColor = refr;
				shb.RefractionRoughness = (float)m.RefractionGlossiness;
				shb.TransparencyColor = transp;
				shb.EmissionColor = emis;


				shb.FresnelIOR = (float)m.FresnelIndexOfRefraction;
				shb.IOR = (float)m.IndexOfRefraction;
				shb.Roughness = (float)m.ReflectionGlossiness;
				shb.Reflectivity = reflectivity;
				shb.Metalic = metalic;
				shb.Transparency = (float)m.Transparency;
				shb.Shine = shine;
				shb.Gloss = (float)m.ReflectionGlossiness;

				shb.FresnelReflections = m.FresnelReflections;

				shb.Gamma = gamma;

				shb.Name = m.Name ?? "";

				shb.DiffuseTexture.Amount = 0.0f;
				shb.BumpTexture.Amount = 0.0f;
				shb.TransparencyTexture.Amount = 0.0f;
				shb.EnvironmentTexture.Amount = 0.0f;

				if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Diffuse))
				{
					var difftex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Diffuse);

					BitmapConverter.MaterialBitmapFromEvaluator(ref shb, rm, difftex, RenderMaterial.StandardChildSlots.Diffuse);
					if (shb.HasDiffuseTexture)
					{
						shb.DiffuseTexture.UseAlpha = difftexAlpha;
						shb.DiffuseTexture.Amount = (float)Math.Min(rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Diffuse) / 100.0f, 1.0f);
					}
				}

				if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Bump))
				{
					var bumptex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Bump);
					BitmapConverter.MaterialBitmapFromEvaluator(ref shb, rm, bumptex, RenderMaterial.StandardChildSlots.Bump);
					if (shb.HasBumpTexture)
					{
						shb.BumpTexture.Amount = (float)Math.Min(rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Bump) / 100.0f, 1.0f);
					}
				}

				if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Transparency))
				{
					var transtex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Transparency);
					BitmapConverter.MaterialBitmapFromEvaluator(ref shb, rm, transtex,
						RenderMaterial.StandardChildSlots.Transparency);
					if (shb.HasTransparencyTexture)
					{
						shb.TransparencyTexture.Amount = (float)Math.Min(rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Transparency) / 100.0f, 1.0f);
					}
				}

				if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Environment))
				{
					var envtex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Environment);
					BitmapConverter.MaterialBitmapFromEvaluator(ref shb, rm, envtex,
						RenderMaterial.StandardChildSlots.Environment);
					if (shb.HasEnvironmentTexture)
					{
						shb.EnvironmentTexture.Amount = (float)Math.Min(rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Environment) / 100.0f, 1.0f);
					}
				}

				rm.EndChange();
			}
			else
			{
				crm.BakeParameters();
				shb.Crm = crm;
				shb.CyclesMaterialType = ShaderBody.CyclesMaterial.Xml;
				shb.Gamma = gamma;
				shb.Name = rm.Name ?? "some cycles material";
			}
			return true;
		}

		/// <summary>
		/// Type of shader this represents.
		/// </summary>
		public enum Shader
		{
			Background,
			Diffuse
		}

		public Shader Type { get; set; }

	}

	public class ShaderBody
	{

		public uint Id { get; }

		public ShaderBody(uint id)
		{
			Id = id;
			DiffuseTexture = new CyclesTextureImage();
			BumpTexture = new CyclesTextureImage();
			TransparencyTexture = new CyclesTextureImage();
			EnvironmentTexture = new CyclesTextureImage();
			GiEnvTexture = new CyclesTextureImage();
			BgEnvTexture = new CyclesTextureImage();
			ReflRefrEnvTexture = new CyclesTextureImage();
			CyclesMaterialType = CyclesMaterial.No;
		}
		public ICyclesMaterial Crm { get; set; }
		/// <summary>
		/// Set the CyclesMaterial type
		/// </summary>
		public CyclesMaterial CyclesMaterialType { get; set; }

		/// <summary>
		/// Enumeration of Cycles custom materials.
		/// 
		/// Note: don't forget to update this enumeration for each
		/// custom material that is added.
		///
		/// Enumeration for both material and background (world)
		/// shaders.
		/// </summary>
		public enum CyclesMaterial
		{
			/// <summary>
			/// No is used when the material isn't a Cycles material
			/// </summary>
			No,

			Xml,

			Brick,
			Test,
			FlakedCarPaint,
			BrickCheckeredMortar,
			Translucent,
			PhongTest,

			Glass,
			Diffuse,
			Paint,
			SimplePlastic,
			SimpleMetal,
			Emissive,

			SimpleNoiseEnvironment,
			XmlEnvironment,
		}

		/// <summary>
		/// Set to true if a shadeless effect is wanted (self-illuminating).
		/// </summary>
		public bool Shadeless { get; set; }
		/// <summary>
		/// Get <c>Shadeless</c> as a float value
		/// </summary>
		public float ShadelessAsFloat => Shadeless ? 1.0f : 0.0f;

		/// <summary>
		/// Gamma corrected base color
		/// </summary>
		public float4 BaseColor
		{
			get
			{
				float4 c = DiffuseColor;
				switch (CyclesMaterialType)
				{
					//case CyclesMaterial.SimpleMetal:
					//	c = ReflectionColor;
					//	break;
					case CyclesMaterial.Glass:
						c = TransparencyColor;
						break;
					default:
						c = DiffuseColor;
						break;
				}

				return c ^ Gamma;
			}
		}

		public float4 DiffuseColor { get; set; }

		public bool HasOnlyDiffuseColor => !HasDiffuseTexture
		                                   && !HasBumpTexture
		                                   && !HasTransparencyTexture
		                                   && !HasEmission
		                                   && !Shadeless
		                                   && NoTransparency
		                                   && NoReflectivity;

		public bool HasOnlyDiffuseTexture => HasDiffuseTexture
		                                     && !HasBumpTexture
		                                     && !HasTransparencyTexture
		                                     && !HasEmission
		                                     && !Shadeless
		                                     && NoTransparency
		                                     && NoReflectivity;

		public bool DiffuseAndBumpTexture => HasDiffuseTexture
		                                     && HasBumpTexture
		                                     && !HasTransparencyTexture
		                                     && !HasEmission
		                                     && !Shadeless
		                                     && NoTransparency
		                                     && NoReflectivity;

		public bool HasOnlyReflectionColor => HasReflectivity
		                                      && !HasDiffuseTexture
		                                      && !HasEmission
		                                      && !Shadeless
		                                      && NoTransparency
		                                      && !HasTransparency
		                                      && !HasBumpTexture;

		public float4 SpecularColor { get; set; }
		public float4 ReflectionColor { get; set; }
		public float ReflectionRoughness { get; set; }
		public float ReflectionRoughnessPow2 => ReflectionRoughness * ReflectionRoughness;
		public float4 RefractionColor { get; set; }
		public float RefractionRoughness { get; set; }
		public float RefractionRoughnessPow2 => RefractionRoughness * RefractionRoughness;
		public float4 TransparencyColor { get; set; }
		public float4 EmissionColor { get; set; }
		public bool HasEmission => !EmissionColor.IsZero(false);

		public CyclesTextureImage DiffuseTexture { get; set; }
		public bool HasDiffuseTexture => DiffuseTexture.HasTextureImage;
		public float HasDiffuseTextureAsFloat => HasDiffuseTexture ? 1.0f : 0.0f;
		public CyclesTextureImage BumpTexture { get; set; }
		public bool HasBumpTexture => BumpTexture.HasTextureImage;
		public float HasBumpTextureAsFloat => HasBumpTexture ? 1.0f : 0.0f;
		public CyclesTextureImage TransparencyTexture { get; set; }
		public bool HasTransparencyTexture => TransparencyTexture.HasTextureImage;
		public float HasTransparencyTextureAsFloat => HasTransparencyTexture ? 1.0f : 0.0f;
		public CyclesTextureImage EnvironmentTexture { get; set; }
		public bool HasEnvironmentTexture => EnvironmentTexture.HasTextureImage;
		public float HasEnvironmentTextureAsFloat => HasEnvironmentTexture ? 1.0f : 0.0f;

		public CyclesTextureImage GiEnvTexture { get; set; }
		public bool HasGiEnvTexture => GiEnvTexture.HasTextureImage;
		public float4 GiEnvColor { get; set; }
		public bool HasGiEnv => HasGiEnvTexture || GiEnvColor != null;

		public CyclesTextureImage BgEnvTexture { get; set; }
		public bool HasBgEnvTexture => BgEnvTexture.HasTextureImage;
		public float4 BgEnvColor { get; set; }
		public bool HasBgEnv => HasBgEnvTexture || BgEnvColor != null;

		public CyclesTextureImage ReflRefrEnvTexture { get; set; }
		public bool HasReflRefrEnvTexture => ReflRefrEnvTexture.HasTextureImage;
		public float4 ReflRefrEnvColor { get; set; }
		public bool HasReflRefrEnv => HasReflRefrEnvTexture || ReflRefrEnvColor != null;

		public bool HasUV { get; set; }

		public float FresnelIOR { get; set; }
		public float IOR { get; set; }
		public float Roughness { get; set; }
		public float Reflectivity { get; set; }
		public float Metalic { get; set; }
		public bool NoMetalic => Math.Abs(Metalic) < 0.00001f;
		public float Shine { get; set; }
		public float Gloss { get; set; }
		public float Transparency { get; set; }
		public bool NoTransparency => Math.Abs(Transparency) < 0.00001f;
		public bool HasTransparency => !NoTransparency;
		public bool NoReflectivity => Math.Abs(Reflectivity) < 0.00001f;
		public bool HasReflectivity => !NoReflectivity;

		private float m_gamma;
		public float Gamma
		{
			get { return m_gamma; }
			set
			{
				m_gamma = value;
				if (Crm != null)
				{
					Crm.Gamma = m_gamma;
				}
			}
		}

		public bool FresnelReflections { get; set; }
		public float FresnelReflectionsAsFloat => FresnelReflections ? 1.0f : 0.0f;

		public string Name { get; set; }
		
	}
}
