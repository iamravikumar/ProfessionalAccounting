grammar Query;

/*
 * Parser Rules
 */

voucherDetailQuery
	:	vouchers emit
	|	voucherQuery
	;

emit
	:	EmitMark details
	;

vouchers
	:	vouchers2
	|	voucherQuery
	;

vouchers2
	:	vouchers2 Op=(Union | Subtract) vouchers1
	|	Op=(Union | Subtract)? vouchers1
	;

vouchers1
	:	vouchers0 (Op=Intersect vouchers1)?
	;

vouchers0
	:	CurlyBra voucherQuery CurlyKet
	|	CurlyBra vouchers2 CurlyKet
	;

voucherQuery
	:	details? MatchAllMark? range? CaretQuotedString? PercentQuotedString? VoucherType?
	;

details
	:	details Op=(Union | Subtract) details1
	|	Op=(Union | Subtract)? details1
	;

details1
	:	details0 (Op=Intersect details1)?
	;

details0
	:	detailQuery
	|	RoundBra details RoundKet
	;

detailQuery
	:	UserSpec? VoucherCurrency? TitleKind? title? token? DoubleQuotedString? Floating? Direction?
	;

title
	:	DetailTitle | DetailTitleSubTitle
	;

range
	:	AllDate
	|	Core=rangeCore
	|	SquareBra Core=rangeCore SquareKet
	;

uniqueTime
	:	Core=uniqueTimeCore
	|	SquareBra Core=uniqueTimeCore SquareKet
	;

rangeCore
	:	RangeNull | RangeAllNotNull
	|	Begin=rangeCertainPoint Tilde End=rangeCertainPoint?
	|	Tilde End=rangeCertainPoint
	|	Certain=rangeCertainPoint
	;

uniqueTimeCore
	:	RangeNull
	|	Day=rangeDay
	;

rangePoint
	:	RangeNull
	|	AllDate
	|	rangeCertainPoint
	;

rangeCertainPoint
	:	rangeYear
	|	rangeMonth
	|	rangeWeek
	|	rangeDay
	;

rangeYear
	:	RangeAYear
	;

rangeMonth
	:	(RangeAMonth | RangeDeltaMonth)
	;

rangeWeek
	:	RangeDeltaWeek
	;

rangeDay
	:	RangeADay | RangeDeltaDay
	;

distributedQ
	:	distributedQ Op=(Union | Subtract) distributedQ1
	|	Op=(Union | Subtract)? distributedQ1
	;

distributedQ1
	:	distributedQ0 (Op=Intersect distributedQ1)?
	;

distributedQ0
	:	distributedQAtom
	|	CurlyBra distributedQ CurlyKet
	;

distributedQAtom
	:	UserSpec? Guid? RegexString? PercentQuotedString? (SquareBra SquareBra rangeCore SquareKet SquareKet)?
	;

token
	:	(SingleQuotedString | Guid | Token)
	;

/*
 * Lexer Rules
 */

Floating
	:	'=' '-'? [1-9] ([0-9])* ('.' [0-9]+)?
	|	'=' '-'? '0' '.' [0-9]+
	;

Guid
	:	H H H H H H H H '-' H H H H '-' H H H H '-' H H H H '-' H H H H H H H H H H H H
	;

fragment H
	:	[0-9A-Za-z]
	;

RangeNull
	:	'null'
	;
RangeAllNotNull
	:	'~null'
	;

RangeAYear
	:	'20' [1-2] [0-9]
	;
RangeAMonth
	:	'20' [1-2] [0-9] '0' [1-9]
	|	'20' [1-2] [0-9] '1' [0-2]
	;
RangeDeltaMonth
	:	'0'
	|	'-' [1-9] [0-9]*
	;
RangeADay
	:	'20' [1-2] [0-9] '0' [1-9] [0-3] [0-9]
	|	'20' [1-2] [0-9] '1' [0-2] [0-3] [0-9]
	;
RangeDeltaDay
	:	'.'+
	;
RangeDeltaWeek
	:	','+
	;

VoucherType
	:	'Ordinary' | 'G' | 'General' | 'Carry' | 'Amortization' | 'Depreciation' | 'Devalue' | 'AnnualCarry' | 'Uncertain'
	;

TitleKind
	:	'Asset' | 'Liability' | 'Equity' | 'Revenue' | 'Expense'
	;

UserSpec
	:	'U' US ('&' US)*
	|	'U' SingleQuotedString
	|	'U'
	;

fragment US
	:	[A-Za-z0-9_]+
	;

VoucherCurrency
	:	'@' [a-zA-Z]+
	|	'@@'
	;

CaretQuotedString
	:	'^' ('^^'|~('^'))* '^'
	;

PercentQuotedString
	:	'%' ('%%'|~('%'))* '%'
	;

RegexString
	:	'/' ('\\'.|~('/'|'\\'))* '/'
	;

DoubleQuotedString
	:	'"' ('""'|~('"'))* '"'
	;

SingleQuotedString
	:	'\'' ('\'\''|~('\''))* '\''
	;

DetailTitle
	:	'T' [1-9] [0-9] [0-9] [0-9]
	;

DetailTitleSubTitle
	:	'T' [1-9] [0-9] [0-9] [0-9] [0-9] [0-9]
	;

AllDate
	:	'[]'
	;

Intersect
	:	'*'
	;
Union
	:	'+'
	;
Subtract
	:	'-'
	;
RoundBra
	:	'('
	;
RoundKet
	:	')'
	;
SquareBra
	:	'['
	;
SquareKet
	:	']'
	;
CurlyBra
	:	'{'
	;
CurlyKet
	:	'}'
	;

Direction
	:	'>' | '<'
	;
Tilde
	:	'~' | '~~'
	;

MatchAllMark
	:	'A'
	;

EmitMark
	:	':'
	;

WS
	:	[ \n\r\t] -> channel(HIDDEN)
	;

Token
	:	~[ \n\r\t'"`!~@$%^*=:.,<>()[\]{}+-]+
	;

Evil
	:	'`' | '!'
	;
