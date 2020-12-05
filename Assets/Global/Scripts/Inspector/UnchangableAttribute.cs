using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// This keyword defines attribute that cannot be changed from inspector after game is started.
/// </summary>
public class UnchangableAttribute : PropertyAttribute
{
}