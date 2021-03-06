﻿module LightCheck.Property

open LightCheck.Gen

/// <summary>
/// A generator of values Gen<Result>, in order to make it possible to mix and
/// match Property combinators and Gen computations.
/// </summary>
type Property =
    private
    | Prop of Gen<Result>

and Result =
    { Status : option<bool>
      Stamps : list<string>
      Args   : list<string> }

/// <summary>
/// Returns a value of type Gen Result out of a property. Useful for mixing and
/// matching Property combinators and Gen computations.
/// </summary>
/// <param name="property">A property to extract the Gen Result from.</param>
let evaluate property =
    let (Prop result) = property
    result

let private boolProperty a =
    { Status = Some a
      Stamps = []
      Args   = [] }
    |> Gen.init
    |> Prop

let private unitProperty =
    { Status = None
      Stamps = []
      Args   = [] }
    |> Gen.init
    |> Prop

let private convert candidate =
    match box candidate with
    | :? Lazy<bool> as b -> boolProperty b.Value
    | :? Property   as p -> p
    | :? bool       as b -> boolProperty b
    | _                  -> unitProperty

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
                        |> convert
                        |> evaluate
             return { res with Args = arg.ToString() :: res.Args }
         })

/// <summary>
/// Returns a property that holds under certain conditions. Laws which are
/// simple equations are conveniently represented by boolean function, but in
/// general many laws hold only under certain conditions.
/// This implication combinator represents such conditional laws.
/// </summary>
/// <param name="b">The precondition's predicate result.</param>
/// <param name="a">The actual result, to be turned into a property.</param>
let implies b a =
    if b then a |> convert
    else     () |> convert

/// <summary>
/// Returns a property that holds under certain conditions. Laws which are
/// simple equations are conveniently represented by boolean function, but in
/// general many laws hold only under certain conditions.
/// This implication combinator represents such conditional laws.
/// </summary>
/// <param name="b">The precondition's predicate result.</param>
/// <param name="a">The actual result, to be turned into a property.</param>
let (==>) b a = implies b a

/// <summary>
/// Labels a test case.
/// </summary>
/// <param name="s">The label.</param>
/// <param name="a">The test case.</param>
let label s a =
    a
    |> evaluate
    |> Gen.lift (fun result -> { result with Stamps = s :: result.Stamps })
    |> Prop

/// <summary>
/// Conditionally labels a test case.
/// </summary>
/// <param name="b">
/// The condition to check whether the test case should be labelled.
/// </param>
/// <param name="s">The label.</param>
/// <param name="a">The test case.</param>
let classify b s a =
    if b then a |> label s
    else     () |> convert

/// <summary>
/// Conditionally labels a test case as trivial.
/// </summary>
/// <param name="b">
/// The condition to check whether the test case should be labelled as trivial.
/// </param>
/// <param name="s">The label.</param>
/// <param name="a">The test case.</param>
let trivial b p = classify b "trivial" p

/// <summary>
/// Gathers all values that are passed to it.
/// </summary>
/// <param name="a">The value.</param>
/// <param name="p">The property.</param>
let collect a p = label (a.ToString()) p

