﻿////////////////////////////////////////////////////////////////////////////////

using Markdig.Renderers;
using Markdig.Syntax;

namespace MD.Editor.Renderer
{
    ////////////////////////////////////////////////////////////////////////////////
    /// <see cref="Markdig.Renderers.Html.HtmlBlockRenderer"/>
    /// <see cref="Markdig.Renderers.Normalize.HtmlBlockRenderer"/>

    public class RendererBlockHtml : MarkdownObjectRenderer<RendererMarkdown, HtmlBlock>
    {
        protected override void Write( RendererMarkdown renderer, HtmlBlock block )
        {
            if( !Preferences.StripHTML )
            {
                renderer.WriteLeafRawLines( block );
                renderer.FinishBlock();
            }
        }
    }
}
