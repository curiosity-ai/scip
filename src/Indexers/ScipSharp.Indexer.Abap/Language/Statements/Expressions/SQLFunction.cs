// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_function.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
// Ported by hand: the TS source uses lambdas and data tables, which a mechanical translation does not handle
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

/// <summary>TS <c>{name, value: "any" | "abap_only", optional?}</c>.</summary>
public sealed record SQLNamedParam(string Name, string Value, bool Optional = false);

public class SQLFunction : Expression
{
    private const int VARIADIC = -1;

    public static readonly (string Name, int Params, ABAPRelease V)[] sqlFunctionDefs =
    [
        ("abs", 1, Release.v740sp05),
        ("ceil", 1, Release.v740sp05),
        ("floor", 1, Release.v740sp05),
        ("div", 2, Release.v740sp05),
        ("mod", 2, Release.v740sp05),
        ("coalesce", VARIADIC, Release.v740sp05),
        ("concat", 2, Release.v750),
        ("length", 1, Release.v750),
        ("ltrim", 2, Release.v750),
        ("rtrim", 2, Release.v750),
        ("right", 2, Release.v750),
        ("replace", 3, Release.v750),
        ("round", 2, Release.v750),
        ("substring", 3, Release.v750),
        ("lpad", 3, Release.v750),
        ("instr", 2, Release.v750),
        ("concat_with_space", 3, Release.v751),
        ("rpad", 3, Release.v751),
        ("left", 2, Release.v751),
        ("lower", 1, Release.v751),
        ("upper", 1, Release.v751),
        ("division", 3, Release.v751),
        ("bintohex", 1, Release.v752),
        ("hextobin", 1, Release.v752),
        ("dats_is_valid", 1, Release.v754),
        ("dats_days_between", 2, Release.v754),
        ("dats_add_days", 2, Release.v754),
        ("dats_add_months", 2, Release.v754),
        ("uuid", 0, Release.v754),
        ("utcl_current", 0, Release.v754),
        ("tstmp_current_utctimestamp", 0, Release.v757),
        ("tstmp_is_valid", 1, Release.v757),
        ("tims_is_valid", 1, Release.v757),
        ("utcl_add_seconds", 2, Release.v757),
        ("utcl_seconds_between", 2, Release.v757),
        ("datn_days_between", 2, Release.v757),
        ("datn_add_days", 2, Release.v757),
        ("datn_add_months", 2, Release.v757),
        ("as_geo_json", 1, Release.v757),
        ("allow_precision_loss", 1, Release.v757),
        ("to_clob", 1, Release.v757),
        ("to_blob", 1, Release.v757),
        ("grouping", 1, Release.v757),
        ("extract_year", 1, Release.v758),
        ("extract_month", 1, Release.v758),
        ("extract_day", 1, Release.v758),
        ("extract_hour", 1, Release.v758),
        ("extract_minute", 1, Release.v758),
        ("extract_second", 1, Release.v758),
        ("days_between", 2, Release.v758),
        ("add_days", 2, Release.v758),
        ("add_months", 2, Release.v758),
        ("monthname", 1, Release.v758),
        ("dayname", 1, Release.v758),
        ("initcap", 1, Release.v758),
        ("is_valid", 1, Release.v758),
        ("weekday", 1, Release.v758),
        ("locate", VARIADIC, Release.v758),
        ("greatest", VARIADIC, Release.v758),
        ("least", VARIADIC, Release.v758),
        ("hierarchy_composite_id", VARIADIC, Release.v758),
        ("sin", 1, Release.v758),
        ("cos", 1, Release.v758),
        ("tan", 1, Release.v758),
        ("asin", 1, Release.v758),
        ("acos", 1, Release.v758),
        ("atan", 1, Release.v758),
        ("sinh", 1, Release.v758),
        ("cosh", 1, Release.v758),
        ("tanh", 1, Release.v758),
        ("sqrt", 1, Release.v758),
        ("power", 2, Release.v758),
        ("exp", 1, Release.v758),
        ("ln", 1, Release.v758),
        ("log10", 1, Release.v758),
        ("log", 2, Release.v758),
        ("utctolocal", 2, Release.v758),
        ("localtoutc", 2, Release.v758),
        ("is_masked", 1, Release.v758),
    ];

    public static readonly (string Name, ABAPRelease V, SQLNamedParam[] Params)[] sqlNamedFunctionDefs =
    [
        ("abap_user_timezone", Release.v757, [new SQLNamedParam("user", "any", true), new SQLNamedParam("client", "any", true), new SQLNamedParam("on_error", "abap_only", true)]),
        ("abap_system_timezone", Release.v757, [new SQLNamedParam("client", "any", true), new SQLNamedParam("on_error", "abap_only", true)]),
        ("dats_tims_to_tstmp", Release.v757, [new SQLNamedParam("date", "any", false), new SQLNamedParam("time", "any", false), new SQLNamedParam("tzone", "any", false), new SQLNamedParam("client", "any", true), new SQLNamedParam("on_error", "abap_only", true)]),
        ("tstmp_add_seconds", Release.v757, [new SQLNamedParam("tstmp", "any", false), new SQLNamedParam("seconds", "any", false), new SQLNamedParam("on_error", "abap_only", true)]),
        ("tstmp_seconds_between", Release.v757, [new SQLNamedParam("tstmp1", "any", false), new SQLNamedParam("tstmp2", "any", false), new SQLNamedParam("on_error", "abap_only", true)]),
        ("tstmp_to_dats", Release.v757, [new SQLNamedParam("tstmp", "any", false), new SQLNamedParam("tzone", "any", true), new SQLNamedParam("client", "any", true), new SQLNamedParam("on_error", "abap_only", true)]),
        ("tstmp_to_tims", Release.v757, [new SQLNamedParam("tstmp", "any", false), new SQLNamedParam("tzone", "any", true), new SQLNamedParam("client", "any", true), new SQLNamedParam("on_error", "abap_only", true)]),
        ("tstmp_to_dst", Release.v757, [new SQLNamedParam("tstmp", "any", false), new SQLNamedParam("tzone", "any", true), new SQLNamedParam("client", "any", true), new SQLNamedParam("on_error", "abap_only", true)]),
        ("tstmpl_to_utcl", Release.v757, [new SQLNamedParam("tstmpl", "any", false), new SQLNamedParam("on_error", "abap_only", true), new SQLNamedParam("on_initial", "abap_only", true)]),
        ("tstmpl_from_utcl", Release.v757, [new SQLNamedParam("utcl", "any", false), new SQLNamedParam("on_null", "abap_only", true)]),
        ("dats_to_datn", Release.v757, [new SQLNamedParam("dats", "any", false), new SQLNamedParam("on_error", "abap_only", true), new SQLNamedParam("on_initial", "abap_only", true)]),
        ("dats_from_datn", Release.v757, [new SQLNamedParam("datn", "any", false), new SQLNamedParam("on_null", "abap_only", true)]),
        ("tims_to_timn", Release.v757, [new SQLNamedParam("tims", "any", false), new SQLNamedParam("on_error", "abap_only", true)]),
        ("tims_from_timn", Release.v757, [new SQLNamedParam("timn", "any", false), new SQLNamedParam("on_null", "abap_only", true)]),
        ("currency_conversion", Release.v757, [new SQLNamedParam("amount", "any", false), new SQLNamedParam("source_currency", "any", false), new SQLNamedParam("target_currency", "any", false), new SQLNamedParam("exchange_rate_date", "any", false), new SQLNamedParam("exchange_rate_type", "any", true), new SQLNamedParam("client", "any", true), new SQLNamedParam("round", "abap_only", true), new SQLNamedParam("decimal_shift", "abap_only", true), new SQLNamedParam("decimal_shift_back", "abap_only", true), new SQLNamedParam("on_error", "abap_only", true)]),
        ("unit_conversion", Release.v758, [new SQLNamedParam("quantity", "any", false), new SQLNamedParam("source_unit", "any", false), new SQLNamedParam("target_unit", "any", false), new SQLNamedParam("client", "any", true), new SQLNamedParam("on_error", "abap_only", true)]),
        ("curr_to_decfloat_amount", Release.v758, [new SQLNamedParam("curr", "any", false), new SQLNamedParam("cuky", "any", false)]),
        ("lang_to_char", Release.v758, [new SQLNamedParam("lang", "any", false)]),
        ("ipow", Release.v758, [new SQLNamedParam("base", "any", false), new SQLNamedParam("exp", "any", false)]),
    ];

    public static readonly (string Name, int Params, ABAPRelease V)[] sqlSpatialFunctionDefs =
    [
        ("st_new_point", VARIADIC, Release.v915),
        ("st_new_pointz", VARIADIC, Release.v915),
        ("st_new_pointm", VARIADIC, Release.v915),
        ("st_new_pointzm", VARIADIC, Release.v915),
        ("st_new_linestring", VARIADIC, Release.v915),
        ("st_new_multilinestring", VARIADIC, Release.v915),
        ("st_new_polygon", VARIADIC, Release.v915),
        ("st_new_multipolygon", VARIADIC, Release.v915),
        ("st_new_multipoint", VARIADIC, Release.v915),
        ("st_new_circularstring", VARIADIC, Release.v915),
        ("st_new_geometrycollection", VARIADIC, Release.v915),
        ("st_srid", VARIADIC, Release.v915),
        ("st_relate", VARIADIC, Release.v915),
        ("st_geomfromtext", VARIADIC, Release.v915),
        ("st_geomfromwkt", VARIADIC, Release.v915),
        ("st_geomfromewkt", VARIADIC, Release.v915),
        ("st_geomfromwkb", VARIADIC, Release.v915),
        ("st_geomfromewkb", VARIADIC, Release.v915),
        ("st_geomfromgeojson", VARIADIC, Release.v915),
        ("st_geomfromesrijson", VARIADIC, Release.v915),
        ("st_geomfromgeohash", VARIADIC, Release.v915),
        ("st_pointfromtext", VARIADIC, Release.v915),
        ("st_pointfromgeohash", VARIADIC, Release.v915),
        ("st_makeline", VARIADIC, Release.v915),
        ("st_makepolygon", VARIADIC, Release.v915),
        ("st_x", VARIADIC, Release.v915),
        ("st_y", VARIADIC, Release.v915),
        ("st_z", VARIADIC, Release.v915),
        ("st_m", VARIADIC, Release.v915),
        ("st_xmax", VARIADIC, Release.v915),
        ("st_xmin", VARIADIC, Release.v915),
        ("st_ymax", VARIADIC, Release.v915),
        ("st_ymin", VARIADIC, Release.v915),
        ("st_zmax", VARIADIC, Release.v915),
        ("st_zmin", VARIADIC, Release.v915),
        ("st_mmax", VARIADIC, Release.v915),
        ("st_mmin", VARIADIC, Release.v915),
        ("st_area", VARIADIC, Release.v915),
        ("st_length", VARIADIC, Release.v915),
        ("st_perimeter", VARIADIC, Release.v915),
        ("st_dimension", VARIADIC, Release.v915),
        ("st_coorddim", VARIADIC, Release.v915),
        ("st_geometrytype", VARIADIC, Release.v915),
        ("st_isempty", VARIADIC, Release.v915),
        ("st_issimple", VARIADIC, Release.v915),
        ("st_isclosed", VARIADIC, Release.v915),
        ("st_isring", VARIADIC, Release.v915),
        ("st_isvalid", VARIADIC, Release.v915),
        ("st_isvalidtrajectory", VARIADIC, Release.v915),
        ("st_is3d", VARIADIC, Release.v915),
        ("st_ismeasured", VARIADIC, Release.v915),
        ("st_invalidreason", VARIADIC, Release.v915),
        ("st_numpoints", VARIADIC, Release.v915),
        ("st_numgeometries", VARIADIC, Release.v915),
        ("st_numinteriorring", VARIADIC, Release.v915),
        ("st_numinteriorrings", VARIADIC, Release.v915),
        ("st_envelope", VARIADIC, Release.v915),
        ("st_boundary", VARIADIC, Release.v915),
        ("st_exteriorring", VARIADIC, Release.v915),
        ("st_startpoint", VARIADIC, Release.v915),
        ("st_endpoint", VARIADIC, Release.v915),
        ("st_centroid", VARIADIC, Release.v915),
        ("st_pointonsurface", VARIADIC, Release.v915),
        ("st_convexhull", VARIADIC, Release.v915),
        ("st_concavehull", VARIADIC, Release.v915),
        ("st_reverse", VARIADIC, Release.v915),
        ("st_force2d", VARIADIC, Release.v915),
        ("st_force3dz", VARIADIC, Release.v915),
        ("st_force3dm", VARIADIC, Release.v915),
        ("st_force4d", VARIADIC, Release.v915),
        ("st_makevalid", VARIADIC, Release.v915),
        ("st_geohash", VARIADIC, Release.v915),
        ("st_aswkt", VARIADIC, Release.v915),
        ("st_aswkb", VARIADIC, Release.v915),
        ("st_asewkt", VARIADIC, Release.v915),
        ("st_asewkb", VARIADIC, Release.v915),
        ("st_asbinary", VARIADIC, Release.v915),
        ("st_astext", VARIADIC, Release.v915),
        ("st_asgeojson", VARIADIC, Release.v915),
        ("st_asesrijson", VARIADIC, Release.v915),
        ("st_assvg", VARIADIC, Release.v915),
        ("st_distance", VARIADIC, Release.v915),
        ("st_withindistance", VARIADIC, Release.v915),
        ("st_contains", VARIADIC, Release.v915),
        ("st_covers", VARIADIC, Release.v915),
        ("st_coveredby", VARIADIC, Release.v915),
        ("st_crosses", VARIADIC, Release.v915),
        ("st_disjoint", VARIADIC, Release.v915),
        ("st_equals", VARIADIC, Release.v915),
        ("st_orderingequals", VARIADIC, Release.v915),
        ("st_intersects", VARIADIC, Release.v915),
        ("st_intersectsfilter", VARIADIC, Release.v915),
        ("st_intersectsrect", VARIADIC, Release.v915),
        ("st_intersectsrectplanar", VARIADIC, Release.v915),
        ("st_overlaps", VARIADIC, Release.v915),
        ("st_touches", VARIADIC, Release.v915),
        ("st_within", VARIADIC, Release.v915),
        ("st_intersection", VARIADIC, Release.v915),
        ("st_union", VARIADIC, Release.v915),
        ("st_difference", VARIADIC, Release.v915),
        ("st_symdifference", VARIADIC, Release.v915),
        ("st_collect", VARIADIC, Release.v915),
        ("st_buffer", VARIADIC, Release.v915),
        ("st_transform", VARIADIC, Release.v915),
        ("st_rotate", VARIADIC, Release.v915),
        ("st_scale", VARIADIC, Release.v915),
        ("st_translate", VARIADIC, Release.v915),
        ("st_translate3d", VARIADIC, Release.v915),
        ("st_simplify", VARIADIC, Release.v915),
        ("st_snaptogrid", VARIADIC, Release.v915),
        ("st_addmeasure", VARIADIC, Release.v915),
        ("st_addpoint", VARIADIC, Release.v915),
        ("st_removepoint", VARIADIC, Release.v915),
        ("st_pointn", VARIADIC, Release.v915),
        ("st_geometryn", VARIADIC, Release.v915),
        ("st_interiorringn", VARIADIC, Release.v915),
        ("st_lineinterpolatepoint", VARIADIC, Release.v915),
        ("st_linelocatepoint", VARIADIC, Release.v915),
        ("st_linesubstring", VARIADIC, Release.v915),
        ("st_locatealong", VARIADIC, Release.v915),
        ("st_locatebetween", VARIADIC, Release.v915),
        ("st_alphashape", VARIADIC, Release.v915),
        ("st_alphashapearea", VARIADIC, Release.v915),
        ("st_alphashapeedge", VARIADIC, Release.v915),
        ("st_closestpointofapproach", VARIADIC, Release.v915),
        ("st_frechetdistance", VARIADIC, Release.v915),
        ("st_hausdorffdistance", VARIADIC, Release.v915),
        ("st_3dlength", VARIADIC, Release.v915),
        ("st_asmvtgeom", VARIADIC, Release.v915),
    ];

    public static readonly (string Name, int Params, ABAPRelease V)[] sqlSpatialAggregateDefs =
    [
        ("st_assvgaggr", VARIADIC, Release.v915),
        ("st_collectaggr", VARIADIC, Release.v915),
        ("st_concavehullaggr", VARIADIC, Release.v915),
        ("st_convexhullaggr", VARIADIC, Release.v915),
        ("st_envelopeaggr", VARIADIC, Release.v915),
        ("st_intersectionaggr", VARIADIC, Release.v915),
        ("st_unionaggr", VARIADIC, Release.v915),
        ("st_alphashapeaggr", VARIADIC, Release.v915),
        ("st_alphashapeareaaggr", VARIADIC, Release.v915),
        ("st_alphashapeedgeaggr", VARIADIC, Release.v915),
    ];

    public override IStatementRunnable GetRunnable()
    {

        var castTypes = AltPrio(
          Seq("CHAR", OptPrio(Seq(Tok<Tokens.ParenLeftW>(), Expr<Integer>(), Tok<Tokens.WParenRightW>()))),
          Seq("DEC", Tok<Tokens.ParenLeftW>(), Expr<Integer>(), ",", Expr<Integer>(), Tok<Tokens.WParenRightW>()),
          Seq("NUMC", OptPrio(Seq(Tok<Tokens.ParenLeftW>(), Expr<Integer>(), Tok<Tokens.WParenRightW>()))),
          "DATS",
          "FLTP",
          "INT1",
          "INT2",
          "INT4",
          "INT8");

        var commaParam = Seq(",", Expr<SQLFunctionInput>());

        IStatementRunnable fn(string name, int @params, ABAPRelease v)
        {
            var nameReg = Regex("^" + name + "$", "i");
            if (@params == 0)
            {
                return Ver(v, Seq(nameReg, Tok<Tokens.ParenLeftW>(), Tok<Tokens.WParenRightW>()), also: AlsoIn.OpenABAP);
            }
            else if (@params == VARIADIC)
            {
                return Ver(v, Seq(nameReg, Tok<Tokens.ParenLeftW>(), Expr<SQLFunctionInput>(), StarPrio(commaParam), Tok<Tokens.WParenRightW>()),
                           also: AlsoIn.OpenABAP);
            }
            else
            {
                var extra = new List<object>();
                for (var i = 1; i < @params; i++)
                {
                    extra.Add(commaParam);
                }
                return Ver(v, SeqOf([nameReg, Tok<Tokens.ParenLeftW>(), Expr<SQLFunctionInput>(), .. extra, Tok<Tokens.WParenRightW>()]),
                           also: AlsoIn.OpenABAP);
            }
        }

        var castInput = AltPrio(Expr<SQLCase>(), Expr<SQLFunctionInput>());
        var cast = Ver(Release.v750, Seq(Regex(@"^cast$", "i"), Tok<Tokens.ParenLeftW>(), castInput, "AS", castTypes, Tok<Tokens.WParenRightW>()),
                       also: AlsoIn.OpenABAP);

        var hostParen = Seq(Tok<Tokens.ParenLeftW>(), Expr<SQLFunctionInput>(), Tok<Tokens.WParenRightW>());
        var hostVar = Seq(Tok<Tokens.WAt>(), AltPrio(Expr<SimpleSource3>(), hostParen));
        var abapOnly = AltPrio(Expr<Constant>(), hostVar);
        IStatementRunnable namedFn(string name, ABAPRelease v, SQLNamedParam[] @params)
        {
            var nameReg = Regex("^" + name + "$", "i");
            object valueFor(SQLNamedParam p) => p.Value == "abap_only" ? abapOnly : Expr<SQLFunctionInput>();
            var kvs = @params.Select(p => Seq(Regex("^" + p.Name + "$", "i"), "=", valueFor(p))).ToArray();
            var anyKv = kvs.Length == 1 ? kvs[0] : AltPrioOf([kvs[0], kvs[1], .. kvs.Skip(2)]);
            var body = OptPrio(Seq(anyKv, StarPrio(Seq(",", anyKv))));
            return Ver(v, Seq(nameReg, Tok<Tokens.ParenLeftW>(), body, Tok<Tokens.WParenRightW>()), also: AlsoIn.OpenABAP);
        }

        return AltPrioOf([
          cast,
          Expr<SQLReplaceRegexpr>(),
          Expr<SQLOccurrencesRegexpr>(),
          Expr<SQLSubstringRegexpr>(),
          Expr<SQLLocateRegexpr>(),
          .. sqlFunctionDefs.Select(d => fn(d.Name, d.Params, d.V)),
          .. sqlNamedFunctionDefs.Select(d => namedFn(d.Name, d.V, d.Params)),
          .. sqlSpatialFunctionDefs.Select(d => fn(d.Name, d.Params, d.V)),
          .. sqlSpatialAggregateDefs.Select(d => fn(d.Name, d.Params, d.V)),
        ]);
    }
}
