module Reader
    open System
    open Tokenizer
    open Types

    type MutableList = System.Collections.Generic.List<Node>
    let inline addToMutableList (lst:MutableList) item = lst.Add(item); lst

    let quote = Symbol("quote")
    let quasiquote = Symbol("quasiquote")
    let unquote = Symbol("unquote")
    let spliceUnquote = Symbol("splice-unquote")
    let deref = Symbol("deref")
    let withMeta = Symbol("with-meta")

    let rec readForm = function
        | OpenParen::rest -> readList [] rest
        | OpenBracket::rest -> readVector (MutableList()) rest
        | OpenBrace::rest -> readMap [] rest
        | SingleQuote::rest -> wrapForm quote rest
        | Backtick::rest -> wrapForm quasiquote rest
        | Tilde::rest -> wrapForm unquote rest
        | SpliceUnquote::rest -> wrapForm spliceUnquote rest
        | At::rest -> wrapForm deref rest
        | Caret::rest -> readMeta rest
        | tokens -> readAtom tokens

    and wrapForm node tokens = 
        match readForm tokens with
        | Some(form), rest -> Some(List([node; form])), rest
        | None, _ -> raise (ReaderError("Expected form, got EOF"))

    and readList acc = function
        | CloseParen::rest -> Some(List(acc |> List.rev)), rest
        | [] -> raise (ReaderError("expected ')', got EOF"))
        | tokens -> 
            match readForm tokens with
            | Some(form), rest -> readList (form::acc) rest
            | None, _ -> raise (ReaderError("expected ')', got EOF"))

    and readVector acc = function
        | CloseBracket::rest -> Some(Vector(acc.ToArray())), rest
        | [] -> raise (ReaderError("expected ']', got EOF"))
        | tokens -> 
            match readForm tokens with
            | Some(form), rest -> readVector (addToMutableList acc form) rest
            | None, _ -> raise (ReaderError("expected ']', got EOF"))

    and readMap acc = function
        | CloseBrace::rest -> Some(Map(acc |> List.rev |> Map.ofList)), rest
        | [] -> raise (ReaderError("Expected '}', got EOF"))
        | tokens -> 
            match readForm tokens with
            | Some(key), rest ->
                match readForm rest with
                | Some(v), rest -> readMap ((key, v)::acc) rest
                | None, _ -> raise (ReaderError("Expected '}', got EOF"))
            | None, _ -> raise (ReaderError("Expected '}', got EOF"))

    and readMeta = function
        | OpenBrace::rest ->
            let meta, rest = readMap [] rest
            match readForm rest with
            | Some(form), rest -> Some(List([withMeta; form; meta.Value])), rest
            | None, _ -> raise (ReaderError("Expected form, got EOF"))
        | _ -> raise (ReaderError("Expected map, got EOF"))

    and readAtom = function
        | Token("nil")::rest -> Some(Nil), rest
        | Token("true")::rest -> Some(Bool(true)), rest
        | Token("false")::rest -> Some(Bool(false)), rest
        | Tokenizer.String(str)::rest -> Some(String(str)), rest
        | Tokenizer.Keyword(kw)::rest -> Some(Keyword(kw)), rest
        | Tokenizer.Number(num)::rest -> Some(Number(Int64.Parse(num))), rest
        | Token(sym)::rest -> Some(Symbol(sym)), rest
        | [] -> None, []
        | _ -> raise (ReaderError("Invalid token"))
        
    let rec readForms acc = function
        | [] -> List.rev acc
        | tokens -> 
            match readForm tokens with
            | Some(form), rest -> readForms (form::acc) rest
            | None, rest -> readForms acc rest

    let read_str str =
        tokenize str |> readForms []
