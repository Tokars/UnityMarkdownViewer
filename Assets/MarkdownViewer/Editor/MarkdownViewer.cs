﻿////////////////////////////////////////////////////////////////////////////////

using Markdig;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace MG.MDV
{
    public interface IActionHandlers
    {
        Texture FetchImage( string url );
        void SelectPage( string url );
    }


    [CustomEditor( typeof( TextAsset ) )]
    public class MarkdownViewer : Editor, IActionHandlers
    {
        public GUISkin      Skin;
        public StyleConfig  StyleConfig;
        public Texture      Placeholder;
        public Texture      IconMarkdown;
        public Texture      IconRaw;
        public Texture      IconBack;
        public Texture      IconForward;

        //------------------------------------------------------------------------------

        private static History mHistory = new History();

        public void SelectPage( string url )
        {
            if( string.IsNullOrEmpty( url ) )
            {
                return;
            }

            var newPath = string.Empty;
            var currentPath = AssetDatabase.GetAssetPath( target );

            if( url.StartsWith( "/" ) )
            {
                newPath = url.Substring( 1 );
            }
            else
            {
                newPath = Utils.PathCombine( Path.GetDirectoryName( currentPath ), url );
            }

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>( newPath );

            if( asset != null )
            {
                mHistory.Add( newPath );
                Selection.activeObject = asset;
            }
            else
            {
                Debug.LogError( $"Could not find asset {newPath}" );
            }
        }

        //------------------------------------------------------------------------------

        class ImageRequest
        {
            public string           URL; // original url
            public UnityWebRequest  Request;

            public ImageRequest( string url )
            {
                URL = url;
                Request = UnityWebRequestTexture.GetTexture( url );
                Request.SendWebRequest();
            }

            public Texture Texture
            {
                get
                {
                    var handler = Request.downloadHandler as DownloadHandlerTexture;
                    return handler?.texture;
                }
            }
        }

        List<ImageRequest> mActiveRequests = new List<ImageRequest>();
        Dictionary<string,Texture> mTextureCache = new Dictionary<string, Texture>();

        private void OnEnable()
        {
            EditorApplication.update += UpdateRequests;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateRequests;
        }

        private string RemapURL( string url )
        {
            if( Regex.IsMatch( url, @"^\w+:", RegexOptions.Singleline ) )
            {
                return url;
            }

            var projectDir = Path.GetDirectoryName( Application.dataPath );

            if( url.StartsWith( "/" ) )
            {
                return $"file:///{projectDir}{url}";
            }

            var assetDir = Path.GetDirectoryName( AssetDatabase.GetAssetPath( target ) );
            return Utils.PathNormalise( $"file:///{projectDir}/{assetDir}/{url}" );
        }

        public Texture FetchImage( string url )
        {
            url = RemapURL( url );

            Texture tex;

            if( mTextureCache.TryGetValue( url, out tex ) )
            {
                return tex;
            }

            mActiveRequests.Add( new ImageRequest( url ) );
            mTextureCache[ url ] = Placeholder;

            return Placeholder;
        }

        void UpdateRequests()
        {
            var req = mActiveRequests.Find( r => r.Request.isDone );

            if( req == null )
            {
                return;
            }

            if( req.Request.isHttpError )
            {
                Debug.LogError( $"HTTP Error: {req.URL} - {req.Request.responseCode} {req.Request.error}" );
                mTextureCache[ req.URL ] = null;
            }
            else if( req.Request.isNetworkError )
            {
                Debug.LogError( $"Network Error: {req.URL} - {req.Request.error}" );
                mTextureCache[ req.URL ] = null;
            }
            else
            {
                mTextureCache[ req.URL ] = req.Texture;
            }

            mActiveRequests.Remove( req );
            Repaint();
        }


        //------------------------------------------------------------------------------
        // TOOD: add preview

        public override bool UseDefaultMargins()
        {
            return false;
        }

        private Editor mDefaultEditor;

        public override void OnInspectorGUI()
        {
            // file extension is .md

            var path = AssetDatabase.GetAssetPath( target );
            var ext  = Path.GetExtension( path );

            if( ".md".Equals( ext, StringComparison.OrdinalIgnoreCase ) )
            {
                ParseDocument( path );
                DrawIMGUI();
            }
            else
            {
                mDefaultEditor = mDefaultEditor ?? CreateEditor( target, Type.GetType( "UnityEditor.TextAssetInspector, UnityEditor" ) );
                mDefaultEditor?.OnInspectorGUI();
            }
        }

        //------------------------------------------------------------------------------

        void ParseDocument( string filename )
        {
            if( mLayout != null )
            {
                return;
            }

            mHistory.OnOpen( filename );

            mLayout = new Layout( new StyleCache( Skin, StyleConfig ), this );

            var renderer = new RendererMarkdown( mLayout );

            // TODO: look at pipeline options ...

            //var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var pipeline = new MarkdownPipelineBuilder().Build();

            pipeline.Setup( renderer );

            var doc = Markdown.Parse( ( target as TextAsset ).text, pipeline );
            renderer.Render( doc );
        }


        //------------------------------------------------------------------------------

        Vector2     mScrollPos;
        Layout      mLayout = null;
        bool        mRaw    = false;
        TextAsset   mText   = null;

        void DrawIMGUI()
        {
            GUI.skin = Skin;
            GUI.enabled = true;

            mText = target as TextAsset;

            // content rect

            GUILayout.FlexibleSpace();
            var rectContainer = GUILayoutUtility.GetLastRect();
            rectContainer.width = Screen.width;


            // clear background

            var rectFullScreen = new Rect( 0.0f, rectContainer.yMin - 4.0f, Screen.width, Screen.height );
            GUI.DrawTexture( rectFullScreen, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false );


            // scroll window

            var padLeft     = 8.0f;
            var padRight    = 4.0f;
            var padHoriz    = padLeft + padRight;
            var scrollWidth = Skin.verticalScrollbar.fixedWidth;
            var minWidth    = rectContainer.width - scrollWidth - padHoriz;
            var maxHeight   = ContentHeight( minWidth );

            var hasScrollbar =  maxHeight >= rectContainer.height;
            var contentWidth = hasScrollbar ? minWidth : rectContainer.width - padHoriz;
            var rectContent  = new Rect( -padLeft, 0.0f, contentWidth, maxHeight );

            // draw content

            DrawToolbar( rectContainer, hasScrollbar ? scrollWidth + padRight : padRight );

            using( var scroll = new GUI.ScrollViewScope( rectContainer, mScrollPos, rectContent ) )
            {
                mScrollPos = scroll.scrollPosition;

                if( mRaw )
                {
                    DrawRaw( rectContent );
                }
                else
                {
                    DrawMarkdown( rectContent );
                }
            }
        }

        //------------------------------------------------------------------------------

        float ContentHeight( float width )
        {
            return mRaw ? Skin.GetStyle( "raw" ).CalcHeight( new GUIContent( mText.text ), width ) : mLayout.Height;
        }

        //------------------------------------------------------------------------------

        void DrawToolbar( Rect rect, float marginRight )
        {
            var style  = GUI.skin.button;
            var size   = style.fixedHeight;
            var btn    = new Rect( rect.xMax - size - marginRight, rect.yMin, size, size );

            if( GUI.Button( btn, mRaw ? IconRaw : IconMarkdown, Skin.button ) )
            {
                mRaw = !mRaw;
            }

            if( mHistory.CanForward )
            {
                btn.x -= size;

                if( GUI.Button( btn, IconForward, Skin.button ) )
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>( mHistory.Forward() );
                }
            }

            if( mHistory.CanBack )
            {
                btn.x -= size;

                if( GUI.Button( btn, IconBack, Skin.button ) )
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>( mHistory.Back() );
                }
            }
        }

        void DrawRaw( Rect rect )
        {
            EditorGUI.SelectableLabel( rect, mText.text, Skin.GetStyle( "raw" ) );
        }

        void DrawMarkdown( Rect rect )
        {
            switch( Event.current.type )
            {
                case EventType.Ignore:
                    break;

                case EventType.ContextClick:
                    var menu = new GenericMenu();
                    menu.AddItem( new GUIContent( "View Source" ), false, () => mRaw = !mRaw );
                    menu.ShowAsContext();

                    break;

                case EventType.Layout:
                    mLayout.Arrange( rect.width );
                    break;

                default:
                    mLayout.Draw();
                    break;
            }
        }
    }
}
