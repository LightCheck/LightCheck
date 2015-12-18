﻿module AutoCheck.Gen

/// <summary>
/// A generator for values of type 'a.
/// </summary>
type Gen<'a> =
    private
    | Gen of (int -> StdGen -> 'a)

/// <summary>
/// Used to construct generators that depend on the size parameter.
/// </summary>
/// <param name="g">A generator for values of type 'a.</param>
let sized g =
    Gen(fun n r ->
        let (Gen m) = g n
        m n r)

/// <summary>
/// Overrides the size parameter. Returns a generator which uses the given size
/// instead of the runtime-size parameter.
/// </summary>
/// <param name="n">The size that's going to override the runtime-size.</param>
let resize n (Gen m) = Gen(fun _ r -> m n r)

/// <summary>
/// Promotes a monadic generator to a generator of monadic values.
/// </summary>
/// <param name="f">The monadic generator </param>
/// <remarks>
/// This is an unsafe combinator for the Gen type. Gen is only morally a monad:
/// two generators that are supposed to be equal will give the same probability
/// distribution, but they might be different as functions from random number
/// seeds to values. QuickCheck, and so AutoCheck, maintains the illusion that
/// a Gen is a probability distribution and does not allow you to distinguish
/// two generators that have the same distribution.
/// The promote function allows you to break this illusion by reusing the same
/// random number seed twice. This is unsafe because by applying the same seed
/// to two morally equal generators, you can see whether they are really equal
/// or not.
/// </remarks>
let promote f =
    Gen(fun n r a ->
        let (Gen m) = f a
        m n r)

/// <summary>
/// Modifies a generator using an integer seed.
/// </summary>
/// <param name="s">The integer seed.</param>
let variant s (Gen m) =
    Gen(fun n r -> m n (Random.variant s r))

/// <summary>
/// Run a generator. The size passed to the generator is up to 30; if you want
/// another size then you should explicitly use 'resize'.
/// </summary>
/// <param name="seed">The seed, in order to get different results on each run.
/// </param>
let generate seed (Gen m) =
    let (size, randomGen) =
        seed
        |> Random.create
        |> Random.range (0, 30)
    m size randomGen

/// <summary>
/// Sequentially compose two actions, passing any value produced by the first
/// as an argument to the second.
/// </summary>
/// <param name="f">
/// The action that produces a value to be passed as argument to the generator.
/// </param>
let bind (Gen m) f =
    Gen(fun n r0 ->
        let r1, r2 = Random.split r0
        let (Gen m') = f (m n r1)
        m' n r2)

/// <summary>
/// Injects a value into a generator.
/// </summary>
/// <param name="a">The value to inject into a generator.</param>
let init a = Gen(fun n r -> a)

/// <summary>
/// Unpacks a function wrapped inside a generator, applying it into a new
/// generator.
/// </summary>
/// <param name="f">The function wrapped inside a generator.</param>
/// <param name="m">The generator, to apply the function to.</param>
let apply f m =
    bind f (fun f' ->
        bind m (fun m' ->
            init (f' m')))

/// <summary>
/// Returns a new generator obtained by applying a function to an existing
/// generator.
/// </summary>
/// <param name="f">The function to apply to an existing generator.</param>
/// <param name="m">The existing generator.</param>
let map f m =
    bind m (fun m' ->
        init (f m'))

module Operators =
    let (>>=) m f = bind m f
    let (<*>) f m = apply f m
    let (<!>) f m = map f m

/// <summary>
/// Returns a new generator obtained by applying a function to an existing
/// generator. Synonym of map.
/// </summary>
/// <param name="f">The function to apply to an existing generator.</param>
/// <param name="m">The existing generator.</param>
let lift f m = map f m

/// <summary>
/// Returns a new generator obtained by applying a function to two existing
/// generators.
/// </summary>
/// <param name="f">The function to apply to the existing generators.</param>
/// <param name="m1">The existing generator.</param>
/// <param name="m2">The existing generator.</param>
let lift2 f m1 m2 =
    apply (apply (init f) m1) m2

/// <summary>
/// Returns a new generator obtained by applying a function to three existing
/// generators.
/// </summary>
/// <param name="f">The function to apply to the existing generators.</param>
/// <param name="m1">The existing generator.</param>
/// <param name="m2">The existing generator.</param>
/// <param name="m3">The existing generator.</param>
let lift3 f m1 m2 m3 =
    apply (apply (apply (init f) m1) m2) m3

/// <summary>
/// Returns a new generator obtained by applying a function to four existing
/// generators.
/// </summary>
/// <param name="f">The function to apply to the existing generators.</param>
/// <param name="m1">The existing generator.</param>
/// <param name="m2">The existing generator.</param>
/// <param name="m3">The existing generator.</param>
/// <param name="m4">The existing generator.</param>
let lift4 f m1 m2 m3 m4 =
    apply (apply (apply (apply (init f) m1) m2) m3) m4

let two   g = lift2 (fun a b     -> a, b)       g g
let three g = lift3 (fun a b c   -> a, b, c)    g g g
let four  g = lift4 (fun a b c d -> a, b, c, d) g g g g

/// <summary>
/// Generates a random element in the given inclusive range, uniformly
/// distributed in the closed interval [lower,upper].
/// </summary>
/// <param name="lower">The lower bound.</param>
/// <param name="upper">The upper bound.</param>
let choose (lower, upper) =
    Gen (fun n r -> r) |> map (Random.range (lower, upper) >> fst)

/// <summary>
/// Generates one of the given values.
/// </summary>
/// <param name="xs">The input list.</param>
/// <remarks>
/// The input list must be non-empty.
/// </remarks>
let elements xs =
    // http://stackoverflow.com/a/1817654/467754
    let flip f x y = f y x
    choose (0, (Seq.length xs) - 1) |> map (flip Seq.item xs)

/// <summary>
/// Randomly uses one of the given generators.
/// </summary>
/// <param name="gens">The input list of generators to use.</param>
/// <remarks>
/// The input list must be non-empty.
/// </remarks>
let oneOf gens =
    let join x = bind x id
    join (elements gens)

[<AutoOpen>]
module Builder =
    type GenBuilder() =
        member this.Bind       (m1, m2) = bind m1 m2
        member this.Return     (x)      = init x
        member this.ReturnFrom (f)      = f

    let gen = GenBuilder()

/// <summary>
/// Chooses one of the given generators, with a weighted random distribution.
/// </summary>
/// <param name="gens">The input list of tuples, in form of a weighted random
/// distribution per generator.
/// </param>
/// <remarks>
/// The input list must be non-empty.
/// </remarks>
let frequency xs =
    let upperBound = List.sumBy fst xs
    let rec pick n =
        function
        | (k, x) :: xs when n <= k -> x
        | (k, x) :: xs             -> pick (n - k) xs

    gen { let! rand = choose (1, upperBound)
          return! pick rand xs }

/// <summary>
/// Adjust the size parameter, by transforming it with the given function.
/// </summary>
/// <param name="f">The function to transform the size parameter.</param>
/// <param name="g">The generator to apply the scaling.</param>
let scale f g = sized (fun n -> resize (f n) g)

/// <summary>
/// Generates some example values.
/// </summary>
/// <param name="seed">The seed use each time the generator runs.</param>
/// <param name="g">The generator to run for generating example values.</param>
let sample seed g = [ for n in [ 0..2..20 ] -> resize n g |> generate seed ]

/// <summary>
/// Tries to generate a value that satisfies a predicate.
/// </summary>
/// <param name="is">The predicate satisfied by the value.</param>
/// <param name="g">The generator to run for creating candidate values.</param>
let suchThatOption is g =
    let rec attempt k n =
        gen {
            match (k, n) with
            | (_, 0) -> return None
            | (k, n) ->
                let! x = resize (2 * k + n) g
                if x |> is then return Some x
                else return! attempt (k + 1) (n - 1)
        }
    sized (max 1 >> attempt 0)

/// <summary>
/// Generates a value that satisfies a predicate.
/// </summary>
/// <param name="is">The predicate satisfied by the value.</param>
/// <param name="g">The generator to run for creating candidate values.</param>
let rec suchThat is g =
    gen {
        let!  option = g |> suchThatOption is
        match option with
        | Some x -> return x
        | None   -> return! sized (fun s -> resize (s + 1) g |> suchThat is)
    }

/// <summary>
/// Takes a list of elements of increasing size, and chooses among an initial
/// segment of the list. The size of this initial segment increases, with the
/// size parameter.
/// </summary>
/// <param name="xs">The input list of elements to choose from.</param>
let growingElements xs =
    let l = Seq.length xs
    sized (fun s ->
        let s' = max 1 s
        let n  = min l s'
        elements (xs |> Seq.take n))

/// <summary>
/// Generates a random permutation of the given list.
/// </summary>
/// <param name="xs">The list to permute.</param>
let rec shuffle xs =
    let pickOne xs = xs |> List.map (fun x -> x, xs |> List.except [ x ])
    gen {
        match xs with
        | [ ] -> return []
        |  _  ->
            let! (y, ys) = xs |> pickOne |> elements
            let!     ys' = shuffle ys
            return (y :: ys')
    }

/// <summary>
/// Returns a new collection containing only the elements of the collection for
/// which the given predicate returns true when run as a generator.
/// </summary>
/// <param name="is">A function to test whether each item in the input sequence
/// should be included in the output. The result of the function is known after
/// is run as a generator.
/// </param>
/// <param name="input">The input sequence.</param>
let filter is input =
    let update x xs =
        gen {
            let! flg = is x
            let! xs' = xs
            return (if flg then x :: xs'
                    else xs')
        }
    init [] |> Seq.foldBack update input

/// <summary>
/// Generates a random subsequence of the given list.
/// </summary>
/// <param name="xs">The list to generate a random subsequence from.</param>
let sublistOf xs =
    filter (fun _ ->
        oneOf [ init true
                init false ]) xs

/// <summary>
/// Generates a list of the given length.
/// </summary>
/// <param name="n">The number of elements to replicate.</param>
/// <param name="g">The generator to replicate.</param>
let vectorOf n g =
    gen {
        return [ for seed in [ 1..n ] -> g |> generate seed ]
    }

/// <summary>
/// Generates a list of random length. The maximum length of the list depends
/// on the size parameter.
/// </summary>
/// <param name="g">The generator from which to create a list from.</param>
let listOf g =
    sized (fun s ->
        gen {
            let! n = choose (0, s)
            return! vectorOf n g
        })

/// <summary>
/// Generates a non-empty list of random length. The maximum length of the list
/// depends on the size parameter.
/// </summary>
/// <param name="g">The generator from which to create a list from.</param>
let nonEmptyListOf g =
    sized (fun s ->
        gen {
            let! n = choose (1, max 1 s)
            return! vectorOf n g
        })

let infiniteSeqOf g =
    gen { return Seq.initInfinite (fun seed -> generate seed g) }
