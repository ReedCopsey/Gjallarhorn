namespace Gjallarhorn.Tests

open System

type Utilities() =
    static member CasesStart : obj [] [] =
        [|
            [|1|] ;
            [|Int32.MinValue|] ;
            [|42.23|] ;
            [|"Foo"|]
        |] 

    static member CasesStartEnd : obj [] [] =
        [|
            [|1 ; 2|] ;
            [|Int32.MinValue ; Int32.MaxValue|] ;
            [|42.23 ; 23.23|] ;
            [|"Foo" ; "Bar"|]
        |] 

    static member CasesStartToString : obj [] [] =
        [|
            [|1 ; "1"|] ;
            [|42.23 ; "42.23"|] ;
            [|"Foo" ; "Foo"|]
        |] 

    static member CasesPairToString : obj [] [] =
        [|
            [|1 ; 2; "1,2"|] ;
            [|42.23 ; -1298.2; "42.23,-1298.2"|] ;
            [|"Foo" ; "Bar" ; "Foo,Bar"|]
        |] 

    static member CasesStartEndToStringPairs : obj [] [] =
        [|
            [|1 ; "1" ; 2 ; "2"|] ;
            [|42.23 ; "42.23" ; 23.398 ; "23.398" |] ;
            [|"Foo" ; "Foo" ; "Bar" ; "Bar" |]
        |] 

    static member CasesPairStartEndToStringPairs : obj [] [] =
        [|
            [|1 ; 2; "1,2" ; 3 ; 4 ; "3,4" |] ;
            [|42.23 ; -1298.2; "42.23,-1298.2" ; -2.01 ; 3948.12 ; "-2.01,3948.12" |] ;
            [|"Foo" ; "Bar" ; "Foo,Bar" ; "Bar" ; "Baz" ; "Bar,Baz" |]
        |] 
