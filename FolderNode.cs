﻿//
//    MCSkin3D, a 3d skin management studio for Minecraft
//    Copyright (C) 2013 Altered Softworks & MCSkin3D Team
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
using System.IO;
using System.Windows.Forms;

namespace MCSkin3D
{
	[Serializable]
	public class FolderNode : TreeNode
	{
		private string _path;

		public FolderNode(string name)
		{
			_path = name;
			Name = Text = new DirectoryInfo(name).Name;
		}

		public DirectoryInfo Directory
		{
			get { return new DirectoryInfo(_path); }
		}

		public override string ToString()
		{
			return Name;
		}

		public void MoveTo(string newFolderString)
		{
			string folderName = Text;

			var newFolder = new DirectoryInfo(newFolderString);

			if (Directory.FullName == newFolder.FullName)
				return;

			while (System.IO.Directory.Exists(newFolder.FullName))
			{
				newFolderString += " - Moved";
				newFolder = new DirectoryInfo(newFolderString);
				//System.Media.SystemSounds.Beep.Play();
				//return;
			}

			Directory.MoveTo(newFolder.FullName);
			Text = Name = newFolder.Name;
			_path = newFolderString;
		}
	}
}