﻿module AutoCheck.Property

open AutoCheck.Gen

/// <summary>
/// A generator of values Gen<Result>, in order to make it possible to mix and
/// match Property combinators and Gen computations.
/// </summary>
type Property =
    private
    | Prop of Gen<Result>

and Result =
    { Status : option<bool>
      Stamp  : list<string>
      Args   : list<string> }

let evaluate property =
    let (Prop result) = property
    result

let private boolProperty a =
    { Status = Some a
      Stamp  = []
      Args   = [] }
    |> Gen.init
    |> Prop

let private unitProperty =
    { Status = None
      Stamp  = []
      Args   = [] }
    |> Gen.init
    |> Prop

let private toProperty candidate =
    match box candidate with
    | :? Lazy<bool> as b -> boolProperty b.Value
    | :? Property   as p -> p
    | :? bool as b -> boolProperty b
    | _            -> unitProperty

/// <summary>
/// Returns a property that holds for all values that can be generated by Gen.
/// </summary>
/// <param name="g">A generator of values for which the property holds.</param>
/// <param name="f">
/// The property for checking whether it holds for all values that can be
/// generated by a given Gen.
/// </param>
let forAll g f =
    Prop(gen {
             let! arg = g
             let! res = f arg
                        |> toProperty
                        |> evaluate
             return { res with Args = arg.ToString() :: res.Args }
         })

let implies b a =
    if b then a |> toProperty
    else     () |> toProperty

let (==>) b a = implies b a
