namespace Scip.Indexer.TypeScript.Syntax;

public sealed partial class Parser
{
    private bool JsxEnabled => Path.GetExtension(_file.FileName).ToLowerInvariant() is ".tsx" or ".jsx" or ".js" or ".mjs" or ".cjs";

    /// <summary>Parses a JSX element or fragment; the current token is the opening <c>&lt;</c>.</summary>
    private Node ParseJsxElementOrFragment()
    {
        var node = ParseJsxElementRaw();
        // Raw parsing leaves the final '>' as the current token.
        node.End = _s.TokenEnd;
        _s.Next();
        return node;
    }

    private void ResetScannerTo(int position)
    {
        _s.Pos = position;
        _s.TokenEnd = position;
        _s.Next();
    }

    /// <summary>Reads a JSX tag/attribute name (identifier parts may contain '-' and ':').</summary>
    private Node ParseJsxName()
    {
        var n = Begin(SK.Identifier);
        if (_s.Kind != T.Ident) { Fail("JSX name expected"); }
        var end = _s.TokenEnd;
        var text = _s.TokenText;
        while (end < _s.Text.Length && (_s.Text[end] == '-' || char.IsLetterOrDigit(_s.Text[end]) || _s.Text[end] is '_' or '$'))
        {
            end++;
        }

        if (end > _s.TokenEnd)
        {
            text = _s.Text[_s.TokenStart..end];
            _s.TokenEnd = end;
            _s.Pos = end;
        }

        n.Text = text;
        _s.Next();
        return Fin(n);
    }

    private Node ParseJsxTagName()
    {
        Node name = ParseJsxName();
        while (IsP(".") || IsP(":"))
        {
            var isNamespace = IsP(":");
            _s.Next();
            var right = ParseJsxName();
            if (isNamespace) { name = Fin(new Node(SK.Identifier, name.Start) { Text = name.Text + ":" + right.Text }); }
            else { name = Fin(new Node(SK.PropertyAccessExpression, name.Start) { Expression = name, Name = right }); }
        }

        return name;
    }

    private Node ParseJsxElementRaw()
    {
        var start = _s.TokenStart;
        ExpectP("<");
        if (IsP(">"))
        {
            // fragment
            var frag = new Node(SK.JsxFragment, start);
            ParseJsxChildren(frag);
            return frag;
        }

        var el = new Node(SK.JsxElement, start);
        el.Name = ParseJsxTagName();
        if (IsP("<")) { ParseTypeArguments(el); }
        while (!IsP(">") && !IsP("/") && _s.Kind != T.Eof)
        {
            if (IsP("{"))
            {
                var spread = Begin(SK.JsxSpreadAttribute);
                _s.Next();
                ExpectP("...");
                spread.Expression = ParseAssignment();
                if (!IsP("}")) { Fail("'}' expected"); }
                _s.Next();
                el.Elements.Add(Fin(spread));
                continue;
            }

            var attr = Begin(SK.JsxAttribute);
            attr.Name = ParseJsxName();
            if (EatP(":")) { attr.Name = ParseJsxName(); }
            if (EatP("="))
            {
                if (_s.Kind == T.String) { var lit = Begin(SK.StringLiteral); lit.Text = _s.TokenText; _s.Next(); attr.Initializer = Fin(lit); }
                else if (IsP("{"))
                {
                    var expr = Begin(SK.JsxExpression);
                    _s.Next();
                    if (!IsP("}")) { expr.Expression = ParseAssignment(); }
                    if (!IsP("}")) { Fail("'}' expected"); }
                    _s.Next();
                    attr.Initializer = Fin(expr);
                }
                else if (IsP("<")) { attr.Initializer = ParseJsxElementOrFragment(); }
                else { Fail("JSX attribute value expected"); }
            }

            el.Elements.Add(Fin(attr));
        }

        if (IsP("/"))
        {
            _s.Next();
            if (!IsP(">")) { Fail("'>' expected"); }
            return el;
        }

        ParseJsxChildren(el);
        return el;
    }

    /// <summary>Current token is the '&gt;' closing an opening tag. Parses children and the closing tag; leaves the
    /// closing '&gt;' as the current token.</summary>
    private void ParseJsxChildren(Node owner)
    {
        var text = _s.Text;
        var pos = _s.TokenEnd;
        while (true)
        {
            if (pos >= text.Length) { Fail("unterminated JSX element"); }
            var c = text[pos];
            if (c == '{')
            {
                ResetScannerTo(pos + 1);
                var expr = new Node(SK.JsxExpression, pos);
                if (IsP("...")) { _s.Next(); }
                if (!IsP("}")) { expr.Expression = ParseExpression(); }
                if (!IsP("}")) { Fail("'}' expected"); }
                expr.End = _s.TokenEnd;
                owner.Elements.Add(expr);
                pos = _s.TokenEnd;
            }
            else if (c == '<')
            {
                var next = pos + 1;
                while (next < text.Length && char.IsWhiteSpace(text[next])) { next++; }
                if (next < text.Length && text[next] == '/')
                {
                    ResetScannerTo(next + 1);
                    if (!IsP(">"))
                    {
                        owner.Extra = ParseJsxTagName();
                    }

                    if (!IsP(">")) { Fail("'>' expected"); }
                    return;
                }

                ResetScannerTo(pos);
                var child = ParseJsxElementRaw();
                child.End = _s.TokenEnd;
                owner.Elements.Add(child);
                pos = _s.TokenEnd;
            }
            else
            {
                pos++;
            }
        }
    }
}
