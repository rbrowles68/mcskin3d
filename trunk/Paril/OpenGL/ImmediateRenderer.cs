﻿//
//    MCSkin3D, a 3d skin management studio for Minecraft
//    Copyright (C) 2011-2012 Altered Softworks & MCSkin3D Team
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using OpenTK.Graphics;

namespace Paril.OpenGL
{
	/// <summary>
	/// OpenGL renderer that renders using immediate mode.
	/// 
	/// </summary>
	public class ImmediateRenderer : Renderer
	{
		public override void RenderMesh(Mesh mesh)
		{
			RenderState.BindTexture(mesh.Texture);

			GL.PushMatrix();

			GL.Translate(mesh.Pivot);
			GL.Rotate(mesh.Rotate.X, 1, 0, 0);
			GL.Rotate(mesh.Rotate.Y, 0, 1, 0);
			GL.Rotate(mesh.Rotate.Z, 0, 0, 1);
			GL.Translate(-mesh.Pivot);

			GL.Translate(mesh.Translate);

			GL.Begin(mesh.Mode);
			foreach (var face in mesh.Faces)
			{
				foreach (var index in face.Indices)
				{
					GL.Color4(face.Colors[index]);
					GL.TexCoord2(face.TexCoords[index]);
					GL.Vertex3(face.Vertices[index]);
				}
			}
			GL.End();

			GL.PopMatrix();
		}
	}
}