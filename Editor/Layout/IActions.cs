////////////////////////////////////////////////////////////////////////////////

using UnityEngine;

namespace MD.Editor.Layout
{
    public interface IActions
    {
        Texture FetchImage( string url );
        void    SelectPage( string url );
    }
}

