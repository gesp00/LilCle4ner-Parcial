using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICleanable
{
    void Clean(CLEAN7Controller cleaner);
    bool IsCleaned { get; }
}
