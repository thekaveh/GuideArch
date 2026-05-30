export class TriangularFuzzyM {
  constructor(
    readonly lower: number,
    readonly modal: number,
    readonly upper: number,
  ) {}

  add(other: TriangularFuzzyM): TriangularFuzzyM {
    return new TriangularFuzzyM(
      this.lower + other.lower,
      this.modal + other.modal,
      this.upper + other.upper,
    );
  }

  sub(other: TriangularFuzzyM): TriangularFuzzyM {
    return new TriangularFuzzyM(
      this.lower - other.lower,
      this.modal - other.modal,
      this.upper - other.upper,
    );
  }

  mul(scalar: number): TriangularFuzzyM {
    return new TriangularFuzzyM(scalar * this.lower, scalar * this.modal, scalar * this.upper);
  }

  div(scalar: number): TriangularFuzzyM {
    if (scalar === 0) throw new Error('TriangularFuzzyM.div: division by zero');
    return new TriangularFuzzyM(this.lower / scalar, this.modal / scalar, this.upper / scalar);
  }

  neg(): TriangularFuzzyM {
    return new TriangularFuzzyM(-this.lower, -this.modal, -this.upper);
  }

  static zero(): TriangularFuzzyM {
    return new TriangularFuzzyM(0, 0, 0);
  }
}
