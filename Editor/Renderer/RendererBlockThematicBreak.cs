﻿////////////////////////////////////////////////////////////////////////////////

using Markdig.Renderers;
using Markdig.Syntax;

namespace MD.Editor.Renderer
{
    ////////////////////////////////////////////////////////////////////////////////
    // <hr/>
    /// <see cref="Markdig.Renderers.Html.ThematicBreakRenderer"/>

    public class RendererBlockThematicBreak : MarkdownObjectRenderer<RendererMarkdown, ThematicBreakBlock>
    {
        protected override void Write( RendererMarkdown renderer, ThematicBreakBlock block )
        {
            renderer.Layout.HorizontalLine();
            renderer.FinishBlock();
        }
    }
}
