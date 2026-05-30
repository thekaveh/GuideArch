import { describe, it, expect } from 'vitest';
import { TriangularFuzzyM } from '../../src/models/triangular-fuzzy.js';

describe('TriangularFuzzyM', () => {
  it('zero() returns (0,0,0)', () => {
    const z = TriangularFuzzyM.zero();
    expect(z.lower).toBe(0);
    expect(z.modal).toBe(0);
    expect(z.upper).toBe(0);
  });

  it('add() is componentwise', () => {
    const a = new TriangularFuzzyM(1, 2, 3);
    const b = new TriangularFuzzyM(4, 5, 6);
    const r = a.add(b);
    expect(r.lower).toBe(5);
    expect(r.modal).toBe(7);
    expect(r.upper).toBe(9);
  });

  it('sub() is componentwise', () => {
    const a = new TriangularFuzzyM(5, 7, 9);
    const b = new TriangularFuzzyM(1, 2, 3);
    const r = a.sub(b);
    expect(r.lower).toBe(4);
    expect(r.modal).toBe(5);
    expect(r.upper).toBe(6);
  });

  it('mul() scales all components', () => {
    const a = new TriangularFuzzyM(1, 2, 3);
    const r = a.mul(3);
    expect(r.lower).toBe(3);
    expect(r.modal).toBe(6);
    expect(r.upper).toBe(9);
  });

  it('mul() by negative flips signs', () => {
    const a = new TriangularFuzzyM(1, 2, 3);
    const r = a.mul(-1);
    expect(r.lower).toBe(-1);
    expect(r.modal).toBe(-2);
    expect(r.upper).toBe(-3);
  });

  it('div() divides all components', () => {
    const a = new TriangularFuzzyM(2, 4, 6);
    const r = a.div(2);
    expect(r.lower).toBe(1);
    expect(r.modal).toBe(2);
    expect(r.upper).toBe(3);
  });

  it('div() throws on zero', () => {
    const a = new TriangularFuzzyM(1, 2, 3);
    expect(() => a.div(0)).toThrow('division by zero');
  });

  it('neg() negates all components', () => {
    const a = new TriangularFuzzyM(1, -2, 3);
    const r = a.neg();
    expect(r.lower).toBe(-1);
    expect(r.modal).toBe(2);
    expect(r.upper).toBe(-3);
  });

  it('add(zero) is identity', () => {
    const a = new TriangularFuzzyM(1.5, 2.5, 3.5);
    const r = a.add(TriangularFuzzyM.zero());
    expect(r.lower).toBe(1.5);
    expect(r.modal).toBe(2.5);
    expect(r.upper).toBe(3.5);
  });
});
